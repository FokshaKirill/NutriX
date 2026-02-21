// navigation.js - Логика для гамбургер меню и навигации

document.addEventListener('DOMContentLoaded', function() {
    // Элементы
    const navbarCollapse = document.getElementById('navbarContent');
    const navbarToggler = document.querySelector('.navbar-toggler');
    const body = document.body;

    if (!navbarCollapse) return;

    // Функция для закрытия меню
    function closeMenu() {
        if (navbarCollapse.classList.contains('show')) {
            const bsCollapse = new bootstrap.Collapse(navbarCollapse, {
                toggle: false
            });
            bsCollapse.hide();
        }
    }

    // Предотвращение прокрутки body когда меню открыто (только на мобильных)
    navbarCollapse.addEventListener('show.bs.collapse', function() {
        if (window.innerWidth < 992) {
            body.style.overflow = 'hidden';
        }
    });

    navbarCollapse.addEventListener('hide.bs.collapse', function() {
        body.style.overflow = '';
    });

    // Закрытие меню при клике на overlay (темную область)
    navbarCollapse.addEventListener('click', function(e) {
        // Проверяем, что клик был именно по overlay (::before псевдоэлементу)
        const rect = navbarCollapse.getBoundingClientRect();
        if (e.clientX < rect.left) {
            closeMenu();
        }
    });

    // Закрытие при клике на кнопку закрытия (крестик)
    document.addEventListener('click', function(e) {
        if (navbarCollapse.classList.contains('show') && window.innerWidth < 992) {
            const rect = navbarCollapse.getBoundingClientRect();

            // Область кнопки закрытия (правый верхний угол)
            const closeButtonArea = {
                top: rect.top,
                right: rect.right,
                bottom: rect.top + 60,
                left: rect.right - 60
            };

            // Проверяем попадание клика в область крестика
            if (e.clientX >= closeButtonArea.left &&
                e.clientX <= closeButtonArea.right &&
                e.clientY >= closeButtonArea.top &&
                e.clientY <= closeButtonArea.bottom) {
                closeMenu();
            }
        }
    });

    // Закрытие меню при клике на ссылку навигации (только на мобильных)
    const navLinks = navbarCollapse.querySelectorAll('.nav-link');
    navLinks.forEach(link => {
        link.addEventListener('click', function() {
            if (window.innerWidth < 992) {
                // Небольшая задержка для визуального эффекта
                setTimeout(closeMenu, 150);
            }
        });
    });

    // Закрытие меню при изменении размера окна на десктоп
    let resizeTimer;
    window.addEventListener('resize', function() {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function() {
            if (window.innerWidth >= 992) {
                closeMenu();
                body.style.overflow = '';
            }
        }, 250);
    });

    // Закрытие меню по клавише Escape
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape' && navbarCollapse.classList.contains('show')) {
            closeMenu();
        }
    });

    // ==========================================
    // ПОДСВЕТКА АКТИВНОЙ СТРАНИЦЫ
    // ==========================================

    const currentPath = window.location.pathname.toLowerCase();

    navLinks.forEach(link => {
        const linkPath = link.getAttribute('href');

        if (!linkPath) return;

        // Точное совпадение пути
        if (linkPath === currentPath) {
            link.classList.add('active');
            return;
        }

        // Частичное совпадение (для подразделов)
        // Например, /Recipe/Details тоже подсветит ссылку /Recipe
        const linkPathSegments = linkPath.split('/').filter(Boolean);
        const currentPathSegments = currentPath.split('/').filter(Boolean);

        if (linkPathSegments.length > 0 && currentPathSegments.length > 0) {
            if (linkPathSegments[0].toLowerCase() === currentPathSegments[0].toLowerCase()) {
                link.classList.add('active');
            }
        }
    });

    // ==========================================
    // ПЛАВНАЯ ПРОКРУТКА ДЛЯ ЯКОРНЫХ ССЫЛОК
    // ==========================================

    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            const href = this.getAttribute('href');

            // Игнорируем пустые якоря и data-bs-toggle
            if (href === '#' || this.hasAttribute('data-bs-toggle')) return;

            const target = document.querySelector(href);
            if (target) {
                e.preventDefault();

                // Закрываем меню на мобильных
                if (window.innerWidth < 992) {
                    closeMenu();
                }

                // Плавная прокрутка
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });

    // ==========================================
    // ПОИСК В HEADER (если нужна логика)
    // ==========================================

    const searchForm = document.querySelector('.search-form');
    const searchInput = searchForm?.querySelector('input[type="search"]');

    if (searchForm && searchInput) {
        searchForm.addEventListener('submit', function(e) {
            e.preventDefault();

            const query = searchInput.value.trim();

            if (query.length === 0) {
                // Можно показать сообщение или просто игнорировать
                return;
            }

            // Здесь можно добавить логику поиска
            // Например, редирект на страницу поиска
            window.location.href = `/Recipe/Search?query=${encodeURIComponent(query)}`;

            // Или AJAX запрос для мгновенного поиска
        });

        // Опционально: Очистка поиска по ESC
        searchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                this.value = '';
                this.blur();
            }
        });
    }

    // ==========================================
    // АНИМАЦИЯ ПОЯВЛЕНИЯ HEADER ПРИ СКРОЛЛЕ
    // ==========================================

    let lastScroll = 0;
    const header = document.querySelector('.header');

    if (header) {
        window.addEventListener('scroll', function() {
            const currentScroll = window.pageYOffset;

            // Скрываем header при скролле вниз, показываем при скролле вверх
            if (currentScroll > lastScroll && currentScroll > 100) {
                // Скроллим вниз
                header.style.transform = 'translateY(-100%)';
                header.style.transition = 'transform 0.3s ease';
            } else {
                // Скроллим вверх
                header.style.transform = 'translateY(0)';
            }

            lastScroll = currentScroll;
        });
    }

    // ==========================================
    // ACCESSIBILITY: Trap focus внутри открытого меню
    // ==========================================

    function trapFocus() {
        if (!navbarCollapse.classList.contains('show')) return;

        const focusableElements = navbarCollapse.querySelectorAll(
            'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
        );

        if (focusableElements.length === 0) return;

        const firstElement = focusableElements[0];
        const lastElement = focusableElements[focusableElements.length - 1];

        document.addEventListener('keydown', function(e) {
            if (e.key !== 'Tab') return;

            if (e.shiftKey) {
                // Shift + Tab
                if (document.activeElement === firstElement) {
                    e.preventDefault();
                    lastElement.focus();
                }
            } else {
                // Tab
                if (document.activeElement === lastElement) {
                    e.preventDefault();
                    firstElement.focus();
                }
            }
        });
    }

    navbarCollapse.addEventListener('shown.bs.collapse', trapFocus);

    // ==========================================
    // УВЕДОМЛЕНИЕ О НОВЫХ ФУНКЦИЯХ (опционально)
    // ==========================================

    // Можно добавить небольшое уведомление для пользователей
    // например, при первом посещении показать подсказку о меню

    const hasSeenMenuHint = localStorage.getItem('hasSeenMenuHint');

    if (!hasSeenMenuHint && window.innerWidth < 992) {
        // Показываем подсказку через 2 секунды после загрузки
        setTimeout(function() {
            if (navbarToggler) {
                // Добавляем небольшую анимацию привлечения внимания
                navbarToggler.style.animation = 'pulse 0.5s ease-in-out 3';

                // Сохраняем, что пользователь видел подсказку
                localStorage.setItem('hasSeenMenuHint', 'true');
            }
        }, 2000);
    }
});

// CSS анимация для pulse (добавить в CSS)
const style = document.createElement('style');
style.textContent = `
    @keyframes pulse {
        0%, 100% { transform: scale(1); }
        50% { transform: scale(1.1); }
    }
`;
document.head.appendChild(style);