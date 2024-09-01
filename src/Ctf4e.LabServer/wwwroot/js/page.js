/*
 * Contains code that should be executed on every page.
 */

// Show toasts
window.addEventListener("load", function() {
    var toastElements = document.getElementsByClassName("toast");
    [...toastElements].map(toastElement => new bootstrap.Toast(toastElement, { delay: 3000 }).show());
});