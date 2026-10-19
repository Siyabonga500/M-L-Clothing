// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Keep the quantity box within the stock of the chosen size and colour.
document.addEventListener('change', e => {
    const form = e.target.closest('form');
    if (!form || !e.target.matches('[data-size-select], [data-color-radio]')) return;
    const qty = form.querySelector('[data-qty-input]');
    const sizeOpt = form.querySelector('[data-size-select]')?.selectedOptions[0];
    const color = form.querySelector('[data-color-radio]:checked');
    let max = parseInt(sizeOpt?.dataset.qty || '1', 10);
    if (color) max = Math.min(max, parseInt(color.dataset.qty || '1', 10));
    if (qty) { qty.max = max; if (parseInt(qty.value, 10) > max) qty.value = max; }
    const label = form.querySelector('[data-color-label]');
    if (label && color) label.textContent = color.value;
});
