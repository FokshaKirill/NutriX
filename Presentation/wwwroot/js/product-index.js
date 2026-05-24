/**
 * product-index.js
 * Логика страницы каталога продуктов: слайдер калорий, модалка создания продукта.
 */

(function () {
    'use strict';

    // ── Слайдер калорийности ─────────────────────────────────────────────────────
    const caloriesRange = document.getElementById('caloriesRange');
    const caloriesValue = document.getElementById('caloriesValue');

    if (caloriesRange && caloriesValue) {
        caloriesRange.addEventListener('input', function () {
            caloriesValue.textContent = this.value + '+ ккал';
        });
    }

    // ── Модалка создания продукта ────────────────────────────────────────────────
    // Зависит от jQuery + Bootstrap (уже подключены глобально через _Layout).

    const createModal = document.getElementById('createProductModal');

    if (createModal && typeof $ !== 'undefined') {
        $('#createProductModal').on('show.bs.modal', function () {
            $.get(window.ProductIndex?.createModalUrl ?? '/Product/CreateModal', function (data) {
                $('#createModalBody').html(data);
                attachCategoryFormHandler();
            });
        });

        $(document).on('submit', '#createProductForm', function (e) {
            e.preventDefault();
            const formData = new FormData(this);

            $.ajax({
                url:         window.ProductIndex?.createModalUrl ?? '/Product/CreateModal',
                type:        'POST',
                data:        formData,
                processData: false,
                contentType: false,
                success: function (result) {
                    if (result.success) {
                        $('#createProductModal').modal('hide');
                        location.reload();
                    } else {
                        $('#createModalBody').html(result);
                        attachCategoryFormHandler();
                    }
                },
                error: function () {
                    alert('Произошла ошибка при сохранении продукта.');
                }
            });
        });
    }

    function attachCategoryFormHandler() {
        $(document).off('submit', '#addCategoryForm');
        $(document).on('submit', '#addCategoryForm', function (e) {
            e.preventDefault();

            $.ajax({
                url:     window.ProductIndex?.createCategoryModalUrl ?? '/Product/CreateCategoryModal',
                type:    'POST',
                data:    $(this).serialize(),
                success: function (result) {
                    if (result.success) {
                        const select = $('#ParentId');
                        select.append(new Option(result.name, result.id, true, true));
                        $('#addCategoryModal').modal('hide');
                        $('#addCategoryForm input[name="name"]').val('');
                    } else {
                        alert(result.message || 'Ошибка при добавлении категории');
                    }
                },
                error: function () {
                    alert('Произошла ошибка при сохранении категории');
                }
            });
        });
    }

})();