// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Keep the quantity box within the stock of the chosen size.
document.addEventListener('change', e => {
    const select = e.target.closest('[data-size-select]');
    if (!select) return;
    const qty = select.closest('form').querySelector('[data-qty-input]');
    const max = parseInt(select.selectedOptions[0]?.dataset.qty || '1', 10);
    if (qty) { qty.max = max; if (parseInt(qty.value, 10) > max) qty.value = max; }
});
