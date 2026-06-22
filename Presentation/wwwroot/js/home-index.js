(function () {
    'use strict';

    // ── Данные со страницы (инициализируются через window.NutriX в Index.cshtml) ──
    const GOALS     = window.NutriX?.goals     ?? { kcal: 0, prot: 0, fat: 0, carb: 0 };
    const MEAL_DATA = window.NutriX?.mealData  ?? {};   // { [id]: {calories,protein,fat,carbs} }

    // ── Дата в шапке меню ───────────────────────────────────────────────────────
    (function setDate() {
        const s  = new Date().toLocaleDateString('ru-RU', {
            weekday: 'long', day: 'numeric', month: 'long'
        });
        const el = document.getElementById('dayDateLabel');
        if (el) el.textContent = s.charAt(0).toUpperCase() + s.slice(1);
    })();

    // ── LocalStorage: ключ сброшен каждый день ──────────────────────────────────
    const DONE_KEY = 'nutrix_done_' + new Date().toISOString().slice(0, 10);

    function getDone() {
        try { return new Set(JSON.parse(localStorage.getItem(DONE_KEY) || '[]')); }
        catch { return new Set(); }
    }

    function saveDone(s) {
        localStorage.setItem(DONE_KEY, JSON.stringify([...s]));
    }

    // ── Стрик: ключи и функции вынесены на уровень модуля,
    //    чтобы быть доступны и из toggleDone, и из recalcProgress, и из restore() ──
    const TODAY_KEY  = new Date().toISOString().slice(0, 10); // "2025-06-17"
    const STREAK_KEY = 'nutrix_streak';

    function loadStreak() {
        try { return JSON.parse(localStorage.getItem(STREAK_KEY) || '{"count":0,"lastDate":""}'); }
        catch { return { count: 0, lastDate: '' }; }
    }

    function saveStreak(obj) {
        localStorage.setItem(STREAK_KEY, JSON.stringify(obj));
    }

    function updateStreak(anyDone) {
        const s = loadStreak();

        if (!anyDone) {
            // Если сегодня ничего не съедено — стрик сегодняшнего дня не засчитан,
            // но прошлые дни не сбрасываем (пользователь может ещё отметить)
            renderStreak(s.count, s.lastDate);
            return;
        }

        if (s.lastDate === TODAY_KEY) {
            // Сегодня уже засчитано
            renderStreak(s.count, s.lastDate);
            return;
        }

        const yesterday = new Date();
        yesterday.setDate(yesterday.getDate() - 1);
        const yesterdayKey = yesterday.toISOString().slice(0, 10);

        const newCount = s.lastDate === yesterdayKey ? s.count + 1 : 1;
        const updated  = { count: newCount, lastDate: TODAY_KEY };
        saveStreak(updated);
        renderStreak(newCount, TODAY_KEY);
    }

    function renderStreak(count, lastDate) {
        const el = document.getElementById('streakCount');
        if (el) el.textContent = count;

        // Подсветить кружок текущего дня если стрик активен сегодня
        const todayDot = document.querySelector('.streak-dot[data-date="' + TODAY_KEY + '"]');
        if (todayDot && lastDate === TODAY_KEY) {
            todayDot.classList.add('done');
        }
    }

    // ── Пересчёт прогресс-баров ──────────────────────────────────────────────────
    function recalcProgress() {
        const done = getDone();
        let kcal = 0, prot = 0, fat = 0, carb = 0;

        for (const id of done) {
            const m = MEAL_DATA[id];
            if (!m) continue;
            kcal += m.calories;
            prot += m.protein;
            fat  += m.fat;
            carb += m.carbs;
        }

        // Стрик пересчитываем один раз после подсчёта итогов, а не на каждой итерации
        updateStreak(done.size > 0);

        function setBar(barId, valId, val, goal) {
            const pct = Math.min(val / Math.max(goal, 1) * 100, 100).toFixed(1);
            const bar = document.getElementById(barId);
            const el  = document.getElementById(valId);
            if (bar) {
                bar.style.width      = pct + '%';
                bar.style.background = val > goal && goal > 0 ? '#e85c5c' : null;
            }
            if (el) el.textContent = val;
        }

        setBar('barKcal', 'eatenKcal', kcal, GOALS.kcal);
        setBar('barProt', 'eatenProt', prot, GOALS.prot);
        setBar('barFat',  'eatenFat',  fat,  GOALS.fat);
        setBar('barCarb', 'eatenCarb', carb, GOALS.carb);
    }

    // ── Переключить "съедено" ────────────────────────────────────────────────────
    window.toggleDone = function (mealId) {
        const done  = getDone();
        const card  = document.getElementById('mealCard_' + mealId);
        const check = document.getElementById('check_'    + mealId);

        if (done.has(mealId)) {
            done.delete(mealId);
            card?.classList.remove('done');
            if (check) check.textContent = '';
        } else {
            done.add(mealId);
            card?.classList.add('done');
            if (check) check.textContent = '✓';
        }

        saveDone(done);
        recalcProgress();
    };

    // ── Восстановить состояние при загрузке страницы ────────────────────────────
    (function restore() {
        for (const id of getDone()) {
            document.getElementById('mealCard_' + id)?.classList.add('done');
            const c = document.getElementById('check_' + id);
            if (c) c.textContent = '✓';
        }
        recalcProgress();
        document.querySelectorAll('.streak-dot.past').forEach(dot => {
            const date    = dot.dataset.date;
            const doneKey = 'nutrix_done_' + date;
            try {
                const arr = JSON.parse(localStorage.getItem(doneKey) || '[]');
                if (arr.length > 0) dot.classList.add('done');
            } catch { /* ignore */ }
        });
    })();

    // ── CSRF токен ───────────────────────────────────────────────────────────────
    function getCsrf() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    // ── Замена рецепта ───────────────────────────────────────────────────────────
    window.replaceMeal = async function (plannedMealId, btn) {
        btn.disabled   = true;
        btn.innerHTML  = '<i class="fas fa-spinner fa-spin"></i>';

        try {
            const params = new URLSearchParams({
                plannedMealId,
                __RequestVerificationToken: getCsrf()
            });

            const res  = await fetch('/MealPlan/ReplaceMeal', { method: 'POST', body: params });
            const data = await res.json();

            if (data.error) {
                alert(data.error);
                return;
            }

            // Обновляем текстовые поля карточки
            const nameEl   = document.getElementById('mealName_'   + plannedMealId);
            const kcalEl   = document.getElementById('mealKcal_'   + plannedMealId);
            const macrosEl = document.getElementById('mealMacros_' + plannedMealId);

            if (nameEl)   nameEl.textContent   = data.name;
            if (kcalEl)   kcalEl.textContent   = data.calories + ' ккал';
            if (macrosEl) macrosEl.textContent  =
                `Б ${data.protein}г · Ж ${data.fat}г · У ${data.carbs}г`;

            // Обновляем изображение
            const card  = document.getElementById('mealCard_' + plannedMealId);
            const imgEl = card?.querySelector('.meal-img');
            const phEl  = card?.querySelector('.meal-img-ph');

            if (data.imageUrl) {
                if (imgEl) {
                    imgEl.src = data.imageUrl;
                    imgEl.alt = data.name;
                } else if (phEl) {
                    phEl.outerHTML =
                        `<img src="${data.imageUrl}" class="meal-img" alt="${data.name}">`;
                }
            } else {
                if (imgEl) imgEl.outerHTML = `<div class="meal-img-ph">🍽</div>`;
            }

            // Обновляем локальный кэш нутриентов
            MEAL_DATA[plannedMealId] = {
                calories: data.calories,
                protein:  data.protein,
                fat:      data.fat,
                carbs:    data.carbs
            };

            // Если блюдо уже было отмечено — пересчитываем прогресс
            if (getDone().has(plannedMealId)) recalcProgress();

            btn.innerHTML = '<i class="fas fa-check" style="color:#5a9668"></i>';
            setTimeout(() => { btn.innerHTML = '<i class="fas fa-sync-alt"></i>'; }, 1500);

        } catch (err) {
            console.error('replaceMeal error:', err);
            alert('Ошибка соединения');
            btn.innerHTML = '<i class="fas fa-sync-alt"></i>';
        } finally {
            btn.disabled = false;
        }
    };

})();