// document.addEventListener('DOMContentLoaded', () => {
//
//     window.showPage = function(page) {
//         document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
//         document.querySelectorAll('.tab').forEach(tab => tab.classList.remove('active'));
//         const pageElement = document.getElementById(`page-${page}`);
//         if (pageElement) pageElement.classList.add('active');
//         const activeTab = document.querySelector(`button[onclick="showPage('${page}')"]`);
//         if (activeTab) activeTab.classList.add('active');
//     };
//
//     window.switchTab = function(tabName) {
//         document.querySelectorAll('.form-tab').forEach(tab => tab.classList.remove('active'));
//         document.querySelectorAll('.form-panel').forEach(panel => panel.classList.remove('active'));
//         const activeTab = document.querySelector(`button[onclick="switchTab('${tabName}')"]`);
//         const activePanel = document.getElementById(`panel-${tabName}`);
//         if (activeTab) activeTab.classList.add('active');
//         if (activePanel) activePanel.classList.add('active');
//     };
//
//     // === Реальный вход ===
//     async function handleLogin() {
//         const email = document.querySelector('#panel-login input[type="email"]').value.trim();
//         const password = document.querySelector('#panel-login input[type="password"]').value.trim();
//
//         if (!email || !password) return alert("Введите email и пароль");
//
//         try {
//             const response = await fetch('/api/auth/login', {
//                 method: 'POST',
//                 headers: { 'Content-Type': 'application/json' },
//                 body: JSON.stringify({ email, password })
//             });
//
//             const data = await response.json();
//
//             if (response.ok) {
//                 showSuccessMessage(loginMsg, data.message || "Успешный вход!");
//
//                 // Небольшая задержка для красоты
//                 setTimeout(() => {
//                     showPage('account');
//                 }, 1200);
//             } else {
//                 alert(data.message || "Ошибка входа");
//             }
//         } catch (err) {
//             alert("Не удалось соединиться с сервером");
//         }
//     }
//
//     // === Реальная регистрация ===
//     async function handleRegister() {
//         const usernameField = document.querySelector('#panel-register input[type="text"]');
//         const emailField = document.querySelector('#panel-register input[type="email"]');
//         const passwordField = document.querySelector('#panel-register input[type="password"]');
//         const registerMsg = document.getElementById('register-msg');
//
//         const username = usernameField?.value.trim();
//         const email = emailField?.value.trim();
//         const password = passwordField?.value.trim();
//
//         if (!username || !email || !password) {
//             alert("Заполните все поля");
//             return;
//         }
//
//         if (password.length < 6) {
//             alert("Пароль минимум 6 символов");
//             return;
//         }
//
//         try {
//             const response = await fetch('/api/auth/register', {
//                 method: 'POST',
//                 headers: { 'Content-Type': 'application/json' },
//                 body: JSON.stringify({ username, email, password })
//             });
//
//             const data = await response.json();
//
//             if (response.ok) {
//                 localStorage.setItem('authToken', data.token);
//                 if (data.user) localStorage.setItem('currentUser', JSON.stringify(data.user));
//
//                 if (registerMsg) {
//                     registerMsg.textContent = "Аккаунт создан!";
//                     registerMsg.style.display = 'block';
//                 }
//                 setTimeout(() => switchTab('login'), 2000);
//             } else {
//                 alert(data.message || "Ошибка регистрации");
//             }
//         } catch (err) {
//             console.error(err);
//             alert("Ошибка соединения с сервером");
//         }
//     }
//
//     // === Выход ===
//     window.logout = async function() {
//         if (!confirm("Выйти из аккаунта?")) return;
//
//         try {
//             await fetch('/api/auth/logout', {
//                 method: 'POST',
//                 headers: { 'Authorization': `Bearer ${localStorage.getItem('authToken') || ''}` }
//             });
//         } catch (e) {
//             console.error("Ошибка при выходе:", e);
//         } finally {
//             localStorage.removeItem('authToken');
//             localStorage.removeItem('currentUser');
//             showPage('auth');
//         }
//     };
//
//     // === Google ===
//     function handleGoogleLogin() {
//         window.location.href = '/api/auth/google';
//     }
//
//     // === Привязка событий ===
//     const loginButton = document.querySelector('#panel-login .btn-primary');
//     if (loginButton) loginButton.addEventListener('click', handleLogin);
//
//     const registerButton = document.querySelector('#panel-register .btn-primary');
//     if (registerButton) registerButton.addEventListener('click', handleRegister);
//
//     document.querySelectorAll('.btn-google').forEach(btn => {
//         btn.addEventListener('click', handleGoogleLogin);
//     });
//
//     const logoutButton = document.querySelector('.btn-logout');
//     if (logoutButton) logoutButton.addEventListener('click', logout);
//
//     document.querySelectorAll('.field input').forEach(input => {
//         input.addEventListener('keypress', (e) => {
//             if (e.key === 'Enter') {
//                 if (input.closest('#panel-login')) handleLogin();
//                 else handleRegister();
//             }
//         });
//     });
//
//     console.log('%cВкусняшки — загружено ✅', 'color: #86bf92; font-weight: 600;');
// });

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