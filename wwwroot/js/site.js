// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

window.showToast = function (message, type = "success") {
    const container = document.getElementById("toast-container");
    if (!container) return;
    const id = `toast-${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const icon = type === "danger" ? "bi-x-circle" : type === "warning" ? "bi-exclamation-circle" : "bi-check-circle";
    container.insertAdjacentHTML("beforeend", `
        <div id="${id}" class="toast text-bg-${type === "danger" ? "danger" : type === "warning" ? "warning" : "success"} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex"><div class="toast-body"><i class="bi ${icon} me-2"></i><span class="toast-message"></span></div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div>
        </div>`);
    const element = document.getElementById(id);
    element.querySelector(".toast-message").textContent = message;
    element.addEventListener("hidden.bs.toast", () => { bootstrap.Toast.getInstance(element)?.dispose(); element.remove(); }, { once: true });
    const toast = bootstrap.Toast.getOrCreateInstance(element, { delay: 5000 });
    toast.show();
};

(() => {
    const html = document.documentElement;
    let savedTheme = null;
    try { savedTheme = localStorage.getItem("demomvc-theme"); } catch (_) {}
    // Chỉ trang có nút sáng/tối (khung quản trị) dùng chế độ tối; trang đăng nhập/đăng ký luôn sáng
    // vì thẻ đăng nhập có màu cố định, áp nền tối vào ô nhập sẽ lệch màu.
    const themeToggle = document.getElementById("theme-toggle");
    if (themeToggle && (savedTheme === "dark" || savedTheme === "light")) html.setAttribute("data-bs-theme", savedTheme);

    themeToggle?.addEventListener("click", () => {
        const theme = html.getAttribute("data-bs-theme") === "dark" ? "light" : "dark";
        html.setAttribute("data-bs-theme", theme);
        try { localStorage.setItem("demomvc-theme", theme); } catch (_) {}
    });

    // Khung trang ArchitectUI: các trạng thái được bật/tắt bằng class trên .app-container.
    const container = document.getElementById("app-container");
    if (!container) return;
    const collapseButton = document.getElementById("collapse-sidebar");
    const mobileButton = document.getElementById("mobile-sidebar");
    const headerButton = document.getElementById("mobile-header");
    const headerContent = document.getElementById("app-header-content");

    // Thu gọn thanh bên trên máy tính: chỉ còn icon, rê chuột để xem tên mục.
    const setCollapsed = collapsed => {
        container.classList.toggle("closed-sidebar", collapsed);
        collapseButton?.classList.toggle("is-active", collapsed);
        collapseButton?.setAttribute("aria-expanded", String(!collapsed));
    };
    const setMobileOpen = open => {
        container.classList.toggle("sidebar-mobile-open", open);
        mobileButton?.classList.toggle("is-active", open);
        mobileButton?.setAttribute("aria-expanded", String(open));
    };
    const setHeaderOpen = open => {
        headerContent?.classList.toggle("header-mobile-open", open);
        headerButton?.classList.toggle("active", open);
        headerButton?.setAttribute("aria-expanded", String(open));
    };

    collapseButton?.addEventListener("click", () => setCollapsed(!container.classList.contains("closed-sidebar")));
    mobileButton?.addEventListener("click", () => setMobileOpen(!container.classList.contains("sidebar-mobile-open")));
    headerButton?.addEventListener("click", () => setHeaderOpen(!headerContent?.classList.contains("header-mobile-open")));

    // Menu trên điện thoại che nội dung: đóng bằng cách chạm ra ngoài hoặc phím Escape.
    document.getElementById("sidebar-backdrop")?.addEventListener("click", () => setMobileOpen(false));
    document.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        setMobileOpen(false);
        setHeaderOpen(false);
    });

    // Như ArchitectUI: màn hình dưới 1250px tự thu gọn thanh bên. Chỉ đổi khi vượt ngưỡng
    // để không ghi đè lựa chọn bấm tay của người dùng mỗi lần đổi kích thước cửa sổ.
    const narrow = window.matchMedia("(max-width: 1249.98px)");
    const phone = window.matchMedia("(max-width: 767.98px)");
    setCollapsed(narrow.matches);
    narrow.addEventListener("change", event => setCollapsed(event.matches));
    phone.addEventListener("change", event => {
        if (!event.matches) {
            setMobileOpen(false);
            setHeaderOpen(false);
        }
    });
})();
