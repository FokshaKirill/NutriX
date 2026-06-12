/**
 * Разметка кнопки (data-атрибуты):
 *   <button class="fav-btn"
 *           data-id="@recipe.Id"
 *           data-fav="@recipe.IsFavorite.ToString().ToLower()">
 *     <i class="@(recipe.IsFavorite ? "fas" : "far") fa-heart"></i>
 *   </button>
 */

(function () {
    "use strict";

    // Токен антиподделки — берём один раз из DOM
    function getToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
    }

    // Определяем, находимся ли мы на странице «Избранное»
    // (чтобы после снятия лайка убирать карточку)
    function isFavoritesPage() {
        return document.body.dataset.page === "favorites"
            || window.location.pathname.toLowerCase().includes("favorites");
    }

    // Анимация «пульс» на кнопке
    function pulse(btn) {
        btn.classList.add("fav-pop");
        btn.addEventListener("animationend", () => btn.classList.remove("fav-pop"), { once: true });
    }

    // Обновить иконку в зависимости от состояния
    function setIcon(btn, isFav) {
        const icon = btn.querySelector("i");
        if (!icon) return;

        if (isFav) {
            icon.classList.remove("far");
            icon.classList.add("fas", "fa-heart");
            btn.classList.add("is-fav");
            btn.setAttribute("title", "Убрать из избранного");
        } else {
            icon.classList.remove("fas");
            icon.classList.add("far", "fa-heart");
            btn.classList.remove("is-fav");
            btn.setAttribute("title", "В избранное");
        }
    }

    // Удалить карточку рецепта из DOM с анимацией
    function removeCard(btn) {
        const card = btn.closest(".recipe-card-wrap");
        if (!card) return;

        card.style.transition = "opacity .35s ease, transform .35s ease";
        card.style.opacity    = "0";
        card.style.transform  = "scale(.9)";
        setTimeout(() => {
            card.remove();
            updateEmptyState();
        }, 360);
    }

    // Показать пустое состояние если карточек не осталось
    function updateEmptyState() {
        const grid  = document.querySelector(".recipes-grid");
        const empty = document.querySelector(".recipes-empty");
        if (!grid || !empty) return;

        const remaining = grid.querySelectorAll(".recipe-card-wrap").length;
        if (remaining === 0) {
            grid.style.display  = "none";
            empty.style.display = "flex";
        }
    }

    // Основной обработчик клика
    document.addEventListener("click", function (e) {
        const btn = e.target.closest(".fav-btn");
        if (!btn) return;

        e.preventDefault();
        e.stopPropagation();

        // Блокируем повторный клик во время запроса
        if (btn.dataset.loading === "true") return;
        btn.dataset.loading = "true";

        const recipeId = btn.dataset.id;
        if (!recipeId) return;

        fetch("/Recipe/ToggleFavorite", {
            method: "POST",
            headers: {
                "Content-Type":                "application/x-www-form-urlencoded",
                "RequestVerificationToken":    getToken(),
                "X-Requested-With":            "XMLHttpRequest"
            },
            body: `id=${encodeURIComponent(recipeId)}&__RequestVerificationToken=${encodeURIComponent(getToken())}`
        })
            .then(r => {
                if (!r.ok) throw new Error("HTTP " + r.status);
                return r.json();
            })
            .then(data => {
                if (data.error) {
                    // Не авторизован — редирект
                    window.location.href = "/account/authpage";
                    return;
                }

                setIcon(btn, data.isFavorite);
                pulse(btn);

                // На странице избранного — убираем карточку если сняли лайк
                if (isFavoritesPage() && !data.isFavorite) {
                    removeCard(btn);
                }

                // Обновим счётчик избранного в шапке если есть
                const counter = document.querySelector(".fav-counter");
                if (counter) {
                    const cur = parseInt(counter.textContent) || 0;
                    counter.textContent = data.isFavorite ? cur + 1 : Math.max(0, cur - 1);
                }
            })
            .catch(err => {
                console.error("Ошибка избранного:", err);
            })
            .finally(() => {
                btn.dataset.loading = "false";
            });
    });

})();