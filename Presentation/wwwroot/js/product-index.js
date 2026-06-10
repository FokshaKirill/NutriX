(function () {
    'use strict';

    // ── Слайдер калорийности
    const caloriesRange = document.getElementById('caloriesRange');
    const caloriesValue = document.getElementById('caloriesValue');

    if (caloriesRange && caloriesValue) {
        caloriesRange.addEventListener('input', function () {
            caloriesValue.textContent = this.value + '+ ккал';
        });
    }

    if (typeof $ !== 'undefined') {

        // МОДАЛКА СОЗДАНИЯ
        $('#createProductModal').on('show.bs.modal', function () {
            $.get(window.ProductIndex?.createModalUrl ?? '/Product/CreateModal', function (data) {
                $('#createModalBody').html(data);
                attachCategoryFormHandler();
            });
        });

        $(document).on('submit', '#createProductModal form', function (e) {
            e.preventDefault();
            submitModalForm(this, '#createProductModal', '#createModalBody', '/Product/CreateModal');
        });
    }

    function submitModalForm(formElement, modalId, bodyId, fallbackUrl) {
        $.ajax({
            url:         $(formElement).attr('action') || fallbackUrl,
            type:        'POST',
            data:        new FormData(formElement),
            processData: false,
            contentType: false,
            success: function (result) {
                if (result.success) {
                    $(modalId).modal('hide');
                    location.reload();
                } else {
                    $(bodyId).html(result);
                }
            },
            error: function () { alert('Ошибка при сохранении изменений.'); }
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
                error: function () { alert('Произошла ошибка при сохранении категории'); }
            });
        });
    }
})();