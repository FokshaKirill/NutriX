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

})();