document.addEventListener('DOMContentLoaded', () => {
    window.logout = async function () {
        if (!confirm('Выйти из аккаунта?')) return;
        try {
            await fetch('/api/auth/logout', { method: 'POST' });
        } catch { } finally {
            localStorage.removeItem('authToken');
            localStorage.removeItem('currentUser');
            window.location.href = '/account/authpage';
        }
    };
});

document.addEventListener('DOMContentLoaded', () => {
    window.switchTab = function(tab) {
        document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
        document.querySelectorAll('.form-panel').forEach(p => p.classList.remove('active'));
        document.querySelector(`button[onclick="switchTab('${tab}')"]`).classList.add('active');
        document.getElementById(`panel-${tab}`).classList.add('active');
        const title = document.getElementById('panel-title');
        const sub   = document.getElementById('panel-subtitle');
        if (tab === 'login') { title.textContent = 'С возвращением'; sub.textContent = 'Войдите в аккаунт, чтобы продолжить'; }
        else { title.textContent = 'Создайте аккаунт'; sub.textContent = 'Присоединяйтесь к сообществу кулинаров'; }
    };

    function showAlert(el, text) {
        if (!el) return;
        el.textContent = text; el.style.display = 'block';
        setTimeout(() => { el.style.display = 'none'; }, 4000);
    }

    async function handleLogin() {
        const email = document.getElementById('login-email').value.trim();
        const password = document.getElementById('login-pass').value.trim();
        const msgEl = document.getElementById('login-msg'), errEl = document.getElementById('login-err');
        if (!email || !password) return showAlert(errEl, 'Введите email и пароль');
        try {
            const res = await fetch('/api/auth/login', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ email, password }) });
            const data = await res.json();
            if (res.ok) {
                if (data.token) localStorage.setItem('authToken', data.token);
                if (data.user)  localStorage.setItem('currentUser', JSON.stringify(data.user));
                showAlert(msgEl, data.message || 'Вход выполнен!');
                setTimeout(() => { window.location.href = '/account'; }, 1200);
            } else showAlert(errEl, data.message || 'Ошибка входа');
        } catch { showAlert(errEl, 'Нет соединения с сервером'); }
    }

    async function handleRegister() {
        const username = document.getElementById('reg-username').value.trim();
        const email    = document.getElementById('reg-email').value.trim();
        const password = document.getElementById('reg-pass').value.trim();
        const msgEl = document.getElementById('reg-msg'), errEl = document.getElementById('reg-err');
        if (!username || !email || !password) return showAlert(errEl, 'Заполните все поля');
        if (password.length < 6) return showAlert(errEl, 'Пароль — минимум 6 символов');
        try {
            const res = await fetch('/api/auth/register', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ username, email, password }) });
            const data = await res.json();
            if (res.ok) {
                if (data.token) localStorage.setItem('authToken', data.token);
                if (data.user)  localStorage.setItem('currentUser', JSON.stringify(data.user));
                showAlert(msgEl, data.message || 'Аккаунт создан!');
                setTimeout(() => switchTab('login'), 2000);
            } else showAlert(errEl, data.message || 'Ошибка регистрации');
        } catch { showAlert(errEl, 'Нет соединения с сервером'); }
    }

    const google = () => { window.location.href = '/api/auth/google'; };
    document.getElementById('btn-login').addEventListener('click', handleLogin);
    document.getElementById('btn-register').addEventListener('click', handleRegister);
    document.getElementById('btn-google-login').addEventListener('click', google);
    document.getElementById('btn-google-reg').addEventListener('click', google);

    document.querySelectorAll('.field input').forEach(inp => {
        inp.addEventListener('keypress', e => {
            if (e.key !== 'Enter') return;
            inp.closest('#panel-login') ? handleLogin() : inp.closest('#panel-register') ? handleRegister() : null;
        });
    });
    console.log('%c🌿 Вкусняшки загружено', 'color:#86bf92;font-weight:600;');
});

(function () {
    const canvas = document.getElementById('rays');
    const ctx    = canvas.getContext('2d');
    let W, H, t = 0;
    let mx = 0.88, my = 0.08, tmx = 0.88, tmy = 0.08;

    function resize() {
        W = canvas.width  = canvas.offsetWidth;
        H = canvas.height = canvas.offsetHeight;
    }
    resize();
    window.addEventListener('resize', resize);

    canvas.parentElement.addEventListener('mousemove', e => {
        const r = canvas.parentElement.getBoundingClientRect();
        tmx = (e.clientX - r.left) / r.width;
        tmy = (e.clientY - r.top)  / r.height;
    });

    function lerp(a, b, n) { return a + (b - a) * n; }

    const RAYS = [
        { baseAngle: 1.510, halfWidth: 0.155, bright: 0.10, freq: 0.000032 },
        { baseAngle: 1.810, halfWidth: 0.255, bright: 0.20, freq: 0.000012 },
        { baseAngle: 2.10, halfWidth: 0.155, bright: 0.30, freq: 0.000042 },
        { baseAngle: 2.21, halfWidth: 0.030, bright: 0.18, freq: 0.000065 },
        { baseAngle: 2.34, halfWidth: 0.072, bright: 0.38, freq: 0.000035 },
        { baseAngle: 2.47, halfWidth: 0.025, bright: 0.14, freq: 0.000078 },
        { baseAngle: 2.58, halfWidth: 0.085, bright: 0.45, freq: 0.000028 },
        { baseAngle: 2.70, halfWidth: 0.020, bright: 0.12, freq: 0.000090 },
        { baseAngle: 2.82, halfWidth: 0.060, bright: 0.32, freq: 0.000048 },
        { baseAngle: 2.94, halfWidth: 0.018, bright: 0.10, freq: 0.000100 },
        { baseAngle: 3.06, halfWidth: 0.048, bright: 0.26, freq: 0.000055 },
        { baseAngle: 3.18, halfWidth: 0.035, bright: 0.20, freq: 0.000070 },
        { baseAngle: 3.30, halfWidth: 0.065, bright: 0.35, freq: 0.000040 },
        { baseAngle: 3.43, halfWidth: 0.022, bright: 0.13, freq: 0.000085 },
        { baseAngle: 3.55, halfWidth: 0.050, bright: 0.28, freq: 0.000050 },
    ];

    function frame() {
        t++;
        mx = lerp(mx, tmx, 0.322);
        my = lerp(my, tmy, 0.222);

        const ox = W * (0.98 + mx * 0.05);
        const oy = H * (my * 0.1);

        ctx.clearRect(0, 0, W, H);
        ctx.fillStyle = '#070d07';
        ctx.fillRect(0, 0, W, H);

        const bigGlow = ctx.createRadialGradient(ox, oy, 2, ox, oy, W * 6.7);
        bigGlow.addColorStop(0,   'rgba(90,170,100,0.2)');
        bigGlow.addColorStop(0.5, 'rgba(50,120,60,0.1)');
        bigGlow.addColorStop(1,   'rgba(0,0,0,0)');
        ctx.fillStyle = bigGlow;
        ctx.fillRect(0, 0, W, H);

        const len = Math.sqrt(W * W + H * H) * 1.5;

        for (let i = 0; i < RAYS.length; i++) {
            const r = RAYS[i];
            const wobble  = Math.sin(t * r.freq * 1000 + i * 1.7) * 0.04;
            const angle   = r.baseAngle + wobble;
            const hw      = r.halfWidth + Math.sin(t * r.freq * 700 + i * 2.3) * 0.01;
            const pulse   = 0.7 + 0.3 * Math.sin(t * r.freq * 500 + i * 1.1);
            const alpha   = r.bright * pulse;

            const a1 = angle - hw, a2 = angle + hw;
            const ex1 = ox + Math.cos(a1) * len, ey1 = oy + Math.sin(a1) * len;
            const ex2 = ox + Math.cos(a2) * len, ey2 = oy + Math.sin(a2) * len;
            const midX = ox + Math.cos(angle) * len * 0.5;
            const midY = oy + Math.sin(angle) * len * 0.5;

            const grad = ctx.createLinearGradient(ox, oy, midX, midY);
            grad.addColorStop(0,   `rgba(180,240,170,${alpha})`);
            grad.addColorStop(0.15,`rgba(120,200,130,${alpha * 0.75})`);
            grad.addColorStop(0.5, `rgba(70,160,80,${alpha * 0.35})`);
            grad.addColorStop(1,   `rgba(30,100,40,0)`);

            ctx.beginPath();
            ctx.moveTo(ox, oy);
            ctx.lineTo(ex1, ey1);
            ctx.lineTo(ex2, ey2);
            ctx.closePath();
            ctx.fillStyle = grad;
            ctx.fill();
        }

        const flare = ctx.createRadialGradient(ox, oy, 0, ox, oy, 100);
        flare.addColorStop(0,   'rgba(230,255,220,0.65)');
        flare.addColorStop(0.25,'rgba(160,230,160,0.25)');
        flare.addColorStop(0.6, 'rgba(90,180,100,0.07)');
        flare.addColorStop(1,   'rgba(0,0,0,0)');
        ctx.fillStyle = flare;
        ctx.beginPath(); ctx.arc(ox, oy, 100, 0, Math.PI * 2); ctx.fill();

        const img = ctx.getImageData(0, 0, W, H);
        const d   = img.data;
        for (let k = 0; k < d.length; k += 20) {
            const n = (Math.random() - 0.5) * 9;
            d[k] += n; d[k+1] += n; d[k+2] += n;
        }
        ctx.putImageData(img, 0, 0);
        requestAnimationFrame(frame);
    }
    frame();
})();
