/**
 * home-index.js
 * Логика главной страницы: прогресс КБЖУ, "съедено", замена рецепта.
 * Данные читаются из window.NutriX, который инициализируется в Index.cshtml.
 */

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