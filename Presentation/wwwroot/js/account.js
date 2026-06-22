/**
 * account.js
 * Логика страницы аккаунта: КБЖУ из localStorage, активация admin.
 * Цели питания читаются из window.AccountData, который инициализируется в Account.cshtml.
 */

(function () {
    'use strict';

    // ── Активация admin ──────────────────────────────────────────────────────────
    window.activateAdmin = async function () {
        const input = document.getElementById('adminSecretInput');
        const btn   = document.getElementById('adminActivateBtn');
        const code  = input?.value?.trim();

        if (!code) { showAdminMsg('Введите код', 'error'); return; }

        btn.disabled    = true;
        btn.textContent = 'Проверяем...';

        try {
            const res  = await fetch('/api/auth/activate-admin', {
                method:  'POST',
                headers: { 'Content-Type': 'application/json' },
                body:    JSON.stringify({ secretCode: code })
            });
            const data = await res.json();

            if (res.ok) {
                showAdminMsg(data.message || 'Готово!', 'success');
                input.value = '';
                setTimeout(() => window.location.reload(), 1500);
            } else {
                showAdminMsg(data.message || 'Ошибка', 'error');
                btn.disabled    = false;
                btn.textContent = 'Активировать';
            }
        } catch {
            showAdminMsg('Нет соединения', 'error');
            btn.disabled    = false;
            btn.textContent = 'Активировать';
        }
    };

    document.getElementById('adminSecretInput')?.addEventListener('keypress', e => {
        if (e.key === 'Enter') window.activateAdmin();
    });

    function showAdminMsg(text, type) {
        const el = document.getElementById('adminActivateMsg');
        if (!el) return;
        el.textContent   = text;
        el.className     = 'admin-activate-msg ' + type;
        el.style.display = 'block';
    }

    (function initKbzu() {
        const d = window.AccountData;
        if (!d) return;

        function setBar(barId, valId, val, goal) {
            const pct = Math.min(val / Math.max(goal, 1) * 100, 100).toFixed(1);
            const bar = document.getElementById(barId);
            const el  = document.getElementById(valId);
            if (bar) {
                bar.style.width      = pct + '%';
            }
            if (el) el.textContent = Math.round(val);
        }

        setBar('barKcal', 'eatenKcal', d.today.kcal, d.goals.kcal);
        setBar('barProt', 'eatenProt', d.today.p,    d.goals.p);
        setBar('barFat',  'eatenFat',  d.today.f,    d.goals.f);
        setBar('barCarb', 'eatenCarb', d.today.c,    d.goals.c);

        // Стрик
        const STREAK_KEY = 'nutrix_streak';
        const TODAY_KEY  = new Date().toISOString().slice(0, 10);
        try {
            const s = JSON.parse(localStorage.getItem(STREAK_KEY) || '{"count":0,"lastDate":""}');
            const el = document.getElementById('streakCount');
            if (el) el.textContent = s.count;
            if (s.lastDate === TODAY_KEY) {
                const dot = document.querySelector('.streak-dot[data-date="' + TODAY_KEY + '"]');
                dot?.classList.add('done');
            }
        } catch { /* ignore */ }

        // Прошлые дни стрика
        document.querySelectorAll('.streak-dot.past').forEach(dot => {
            const key = 'nutrix_done_' + dot.dataset.date;
            try {
                if (JSON.parse(localStorage.getItem(key) || '[]').length > 0)
                    dot.classList.add('done');
            } catch { /* ignore */ }
        });
    })();
})();