(() => {
    const host = document.getElementById("crud-modal-host");
    if (!host) return;

    const setSubmitLoading = (form, loading) => {
        const button = form.querySelector('[type="submit"]');
        if (!button) return;
        button.disabled = loading;
        if (loading) button.dataset.originalHtml = button.innerHTML;
        if (loading) button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Đang xử lý...';
        else if (button.dataset.originalHtml) button.innerHTML = button.dataset.originalHtml;
    };

    const parseValidation = (root) => {
        if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
            jQuery.validator.unobtrusive.parse(root.querySelector("form") || root);
        }
    };

    const installModal = (html) => {
        host.innerHTML = html;
        const modalElement = host.querySelector(".modal");
        if (!modalElement) {
            window.showToast("Dữ liệu trả về không có hộp thoại hợp lệ.", "danger");
            return;
        }
        parseValidation(modalElement);
        window.initPasswordToggles?.(modalElement);
        bootstrap.Modal.getOrCreateInstance(modalElement).show();
    };

    document.addEventListener("click", async (event) => {
        const trigger = event.target.closest("[data-modal-url]");
        if (!trigger) return;
        event.preventDefault();
        try {
            const response = await fetch(trigger.dataset.modalUrl, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (response.status === 403) {
                const result = await response.json();
                window.showToast(result.message || "Bạn không có quyền thực hiện chức năng này.", "danger");
                return;
            }
            if (!response.ok) throw new Error(`Không thể tải dữ liệu (HTTP ${response.status}).`);
            installModal(await response.text());
        } catch (error) {
            window.showToast(error.message || "Không thể tải dữ liệu.", "danger");
        }
    });

    // Ma trận phân quyền: ô đầu dòng bật/tắt mọi action (còn sửa được) của controller đó.
    const syncModuleToggle = (row) => {
        const toggle = row?.querySelector("[data-permission-module-toggle]");
        if (!toggle) return;
        const boxes = [...row.querySelectorAll('input[name="selectedPermissions"]:not(:disabled)')];
        const checked = boxes.filter(box => box.checked).length;
        toggle.checked = boxes.length > 0 && checked === boxes.length;
        toggle.indeterminate = checked > 0 && checked < boxes.length;
    };

    host.addEventListener("change", (event) => {
        const row = event.target.closest("tr");
        if (event.target.matches("[data-permission-module-toggle]")) {
            row.querySelectorAll('input[name="selectedPermissions"]:not(:disabled)').forEach(box => {
                box.checked = event.target.checked;
            });
            event.target.indeterminate = false;
        } else if (event.target.matches('input[name="selectedPermissions"]')) {
            syncModuleToggle(row);
        }
    });

    host.addEventListener("click", (event) => {
        const button = event.target.closest("[data-permission-select-all]");
        if (!button) return;
        const boxes = [...host.querySelectorAll('input[name="selectedPermissions"]:not(:disabled)')];
        const selectAll = boxes.some(box => !box.checked);
        boxes.forEach(box => { box.checked = selectAll; });
        host.querySelectorAll("tr").forEach(syncModuleToggle);
        button.textContent = selectAll ? "Bỏ chọn tất cả" : "Chọn tất cả";
    });

    host.addEventListener("shown.bs.modal", () => {
        host.querySelectorAll("tr").forEach(syncModuleToggle);
    });

    host.addEventListener("submit", async (event) => {
        const form = event.target.closest("[data-ajax-modal-form], form");
        if (!form || !host.contains(form)) return;
        event.preventDefault();
        setSubmitLoading(form, true);
        try {
            const response = await fetch(form.action, {
                method: form.method || "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const contentType = response.headers.get("content-type") || "";
            if (contentType.includes("application/json")) {
                const result = await response.json();
                if (!response.ok || result.success === false) {
                    window.showToast(result.message || "Bạn không có quyền thực hiện chức năng này.", "danger");
                    setSubmitLoading(form, false);
                    return;
                }
                const modalElement = host.querySelector(".modal.show");
                if (modalElement) {
                    modalElement.addEventListener("hidden.bs.modal", () => {
                        host.innerHTML = "";
                        refreshCrudList();
                    }, { once: true });
                    bootstrap.Modal.getOrCreateInstance(modalElement).hide();
                } else {
                    await refreshCrudList();
                }
                window.showToast(result.message || "Đã lưu thay đổi.", "success");
                return;
            }

            if (response.status === 403) {
                window.showToast("Bạn không có quyền thực hiện chức năng này.", "danger");
                setSubmitLoading(form, false);
                return;
            }
            const html = await response.text();
            installModal(html);
        } catch (error) {
            window.showToast(error.message || "Không thể lưu dữ liệu.", "danger");
            setSubmitLoading(form, false);
        }
    });

    async function refreshCrudList() {
        const list = document.querySelector("[data-crud-list]");
        if (!list) {
            window.location.reload();
            return;
        }
        const response = await fetch(window.location.href, {
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error("Không thể tải lại danh sách.");
        const html = await response.text();
        const page = document.createElement("html");
        page.innerHTML = html;
        const nextList = page.querySelector("[data-crud-list]");
        if (nextList) list.innerHTML = nextList.innerHTML;
        document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(element => {
            bootstrap.Tooltip.getOrCreateInstance(element);
        });
    }
})();
