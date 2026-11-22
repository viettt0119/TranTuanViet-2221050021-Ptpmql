// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

window.showToast = function (message, type = "success") {
    const container = document.getElementById("toast-container");
    if (!container) return;
    const id = `toast-${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const icon = type === "danger" ? "bi-x-circle" : type === "warning" ? "bi-exclamation-circle" : "bi-check-circle";
    container.insertAdjacentHTML("beforeend", `
        <div id="${id}" class="toast text-bg-${type === "danger" ? "danger" : type === "warning" ? "warning" : "success"} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex"><div class="toast-body"><i class="bi ${icon} me-2"></i>${message}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div>
        </div>`);
    const toast = bootstrap.Toast.getOrCreateInstance(document.getElementById(id), { delay: 3500 });
    toast.show();
};

(() => {
    const html = document.documentElement;
    let savedTheme = null;
    try { savedTheme = localStorage.getItem("demomvc-theme"); } catch (_) {}
    if (savedTheme === "dark" || savedTheme === "light") html.setAttribute("data-bs-theme", savedTheme);

    document.getElementById("theme-toggle")?.addEventListener("click", () => {
        const theme = html.getAttribute("data-bs-theme") === "dark" ? "light" : "dark";
        html.setAttribute("data-bs-theme", theme);
        try { localStorage.setItem("demomvc-theme", theme); } catch (_) {}
    });

    document.getElementById("collapse-sidebar")?.addEventListener("click", () => {
        document.getElementById("app-sidebar")?.classList.toggle("collapsed");
        document.getElementById("app-main")?.classList.toggle("sidebar-collapsed");
    });

    document.getElementById("mobile-sidebar")?.addEventListener("click", () => {
        document.getElementById("app-sidebar")?.classList.toggle("show");
    });
})();
