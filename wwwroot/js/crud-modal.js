(() => {
    const host = document.getElementById("crud-modal-host");
    // Vùng nội dung của khung ArchitectUI có z-index riêng (thấp hơn thanh bên và thanh trên):
    // modal nằm bên trong sẽ bị thanh bên che và nằm dưới lớp mờ. Đưa vùng chứa modal ra thẳng <body>.
    if (host && host.parentElement !== document.body) document.body.appendChild(host);
    const ajaxHeaders = { "X-Requested-With": "XMLHttpRequest" };
    let listRequest;
    let openingModal = false;

    const setSubmitLoading = (form, loading) => {
        form.querySelectorAll('[type="submit"]').forEach(button => {
            if (loading) {
                button.dataset.originalHtml = button.innerHTML;
                button.disabled = true;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Đang xử lý...';
            } else {
                button.disabled = false;
                if (button.dataset.originalHtml) button.innerHTML = button.dataset.originalHtml;
            }
        });
    };

    async function checkResponse(response, allowValidation = false) {
        if (response.status === 401 || (response.redirected && new URL(response.url).pathname.toLowerCase().includes("/account/login"))) {
            throw new Error("Phiên đăng nhập đã hết. Vui lòng đăng nhập lại rồi thử lại.");
        }
        if (response.ok || (allowValidation && response.status === 422)) return;
        let message = {
            400: "Yêu cầu không hợp lệ hoặc phiên biểu mẫu đã hết. Vui lòng tải lại trang.",
            403: "Bạn không có quyền thực hiện chức năng này.",
            404: "Bản ghi không còn tồn tại. Vui lòng tải lại danh sách."
        }[response.status] || `Không thể xử lý yêu cầu (HTTP ${response.status}).`;
        if ((response.headers.get("content-type") || "").includes("application/json")) {
            const result = await response.json();
            message = result.message || message;
        }
        throw new Error(message);
    }

    const parseValidation = root => {
        if (window.jQuery?.validator?.unobtrusive) {
            window.jQuery.validator.unobtrusive.parse(root.querySelector("form") || root);
        }
    };

    const focusForm = root => {
        const field = root.querySelector('.input-validation-error, input:not([type="hidden"]):not(:disabled), select:not(:disabled), textarea:not(:disabled)');
        field?.focus();
    };

    const installModal = html => {
        if (!host) throw new Error("Không tìm thấy vùng hiển thị hộp thoại.");
        const page = new DOMParser().parseFromString(html, "text/html");
        const incoming = page.querySelector(".modal");
        if (!incoming?.querySelector(".modal-content")) throw new Error("Dữ liệu trả về không có hộp thoại hợp lệ.");
        let modalElement = host.querySelector(".modal.show");
        if (modalElement) {
            // Giữ Bootstrap instance/backdrop khi server trả lại form có lỗi.
            modalElement.querySelector(".modal-content").innerHTML = incoming.querySelector(".modal-content").innerHTML;
            // Hộp thoại mới có thể khác kích thước và tiêu đề (vd. Chi tiết → Sửa).
            modalElement.querySelector(".modal-dialog").className = incoming.querySelector(".modal-dialog").className;
            const labelledBy = incoming.getAttribute("aria-labelledby");
            if (labelledBy) modalElement.setAttribute("aria-labelledby", labelledBy);
            modalElement.querySelectorAll("tr").forEach(syncModuleToggle);
            focusForm(modalElement);
        } else {
            host.querySelectorAll(".modal").forEach(modal => bootstrap.Modal.getInstance(modal)?.dispose());
            host.replaceChildren(incoming);
            modalElement = incoming;
            modalElement.addEventListener("shown.bs.modal", () => focusForm(modalElement), { once: true });
            bootstrap.Modal.getOrCreateInstance(modalElement).show();
        }
        parseValidation(modalElement);
        window.initPasswordToggles?.(modalElement);
    };

    async function closeModal() {
        const modalElement = host?.querySelector(".modal.show");
        if (!modalElement) return;
        await new Promise(resolve => {
            modalElement.addEventListener("hidden.bs.modal", resolve, { once: true });
            bootstrap.Modal.getOrCreateInstance(modalElement).hide();
        });
        bootstrap.Modal.getInstance(modalElement)?.dispose();
        host.replaceChildren();
    }

    async function loadList(address, historyMode = "replace") {
        const list = document.querySelector("[data-crud-list]");
        if (!list) throw new Error("Không tìm thấy danh sách cần cập nhật.");
        listRequest?.abort();
        const request = new AbortController();
        listRequest = request;
        list.setAttribute("aria-busy", "true");
        try {
            const url = new URL(address, window.location.href);
            const response = await fetch(url, { headers: ajaxHeaders, signal: request.signal });
            await checkResponse(response);
            const page = new DOMParser().parseFromString(await response.text(), "text/html");
            const nextList = page.querySelector("[data-crud-list]");
            if (!nextList) throw new Error("Phản hồi thiếu danh sách. Vui lòng tải lại trang.");
            if (request.signal.aborted) return;
            list.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(element => bootstrap.Tooltip.getInstance(element)?.dispose());
            list.replaceWith(nextList);
            nextList.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(element => bootstrap.Tooltip.getOrCreateInstance(element));
            const pager = nextList.querySelector("[data-page-index]");
            if (pager) {
                url.searchParams.set("page", pager.dataset.pageIndex);
                url.searchParams.set("pageSize", pager.dataset.pageSize);
            }
            // Sau khi xóa hết trang cuối, URL theo trang đã được server đưa về hợp lệ.
            window.history[historyMode === "push" ? "pushState" : "replaceState"]({}, "", url);
        } catch (error) {
            if (error.name !== "AbortError") throw error;
        } finally {
            if (listRequest === request) document.querySelector("[data-crud-list]")?.removeAttribute("aria-busy");
        }
    }

    document.addEventListener("click", async event => {
        const trigger = event.target.closest("[data-modal-url]");
        if (trigger && host) {
            event.preventDefault();
            if (openingModal) return;
            openingModal = true;
            trigger.setAttribute("aria-busy", "true");
            try {
                const response = await fetch(trigger.dataset.modalUrl, { headers: ajaxHeaders });
                await checkResponse(response);
                installModal(await response.text());
            } catch (error) {
                window.showToast(error.message || "Không thể tải dữ liệu.", "danger");
            } finally {
                openingModal = false;
                trigger.removeAttribute("aria-busy");
            }
            return;
        }
        const link = event.target.closest(".pagination a, th a, [data-list-url]");
        if (!link?.closest("[data-crud-list]") || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
        if (link.closest(".disabled") || link.getAttribute("aria-disabled") === "true") { event.preventDefault(); return; }
        if (new URL(link.href).pathname !== window.location.pathname) return;
        event.preventDefault();
        try { await loadList(link.href, "push"); }
        catch (error) { window.showToast(error.message, "danger"); }
    });

    document.addEventListener("submit", async event => {
        const form = event.target;
        const list = form.closest("[data-crud-list]");
        if (!list || form.method.toLowerCase() !== "get") return;
        event.preventDefault();
        const url = new URL(form.action, window.location.href);
        url.search = new URLSearchParams(new FormData(form)).toString();
        url.searchParams.set("page", "1");
        if (!url.searchParams.has("pageSize")) url.searchParams.set("pageSize", list.querySelector("[data-page-size]")?.dataset.pageSize || "10");
        try { await loadList(url, "push"); }
        catch (error) { window.showToast(error.message, "danger"); }
    });

    window.addEventListener("popstate", async () => {
        if (!document.querySelector("[data-crud-list]")) return;
        try { await loadList(window.location.href); }
        catch (error) { window.showToast(error.message, "danger"); }
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

    host?.addEventListener("change", (event) => {
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

    host?.addEventListener("click", (event) => {
        const button = event.target.closest("[data-permission-select-all]");
        if (!button) return;
        const boxes = [...host.querySelectorAll('input[name="selectedPermissions"]:not(:disabled)')];
        const selectAll = boxes.some(box => !box.checked);
        boxes.forEach(box => { box.checked = selectAll; });
        host.querySelectorAll("tr").forEach(syncModuleToggle);
        button.textContent = selectAll ? "Bỏ chọn tất cả" : "Chọn tất cả";
    });

    host?.addEventListener("shown.bs.modal", () => {
        host.querySelectorAll("tr").forEach(syncModuleToggle);
    });

    host?.addEventListener("submit", async event => {
        const form = event.target.closest("form");
        if (!form || !host.contains(form)) return;
        event.preventDefault();
        if (form.dataset.submitting === "true") return;
        if (window.jQuery?.validator && !window.jQuery(form).valid()) return;
        form.dataset.submitting = "true";
        setSubmitLoading(form, true);
        try {
            const response = await fetch(form.action, {
                method: form.method || "POST",
                body: new FormData(form),
                headers: ajaxHeaders
            });
            await checkResponse(response, true);
            if ((response.headers.get("content-type") || "").includes("application/json")) {
                const result = await response.json();
                if (result.success !== true) throw new Error(result.message || "Không thể lưu dữ liệu.");
                await closeModal();
                try {
                    await loadList(window.location.href);
                    window.showToast(result.message || "Đã lưu thay đổi.", "success");
                } catch (error) {
                    window.showToast(`Đã lưu thay đổi, nhưng chưa tải lại được danh sách. ${error.message}`, "warning");
                }
            } else if (response.status === 422) {
                installModal(await response.text());
            } else {
                throw new Error("Phản hồi lưu dữ liệu không hợp lệ. Vui lòng tải lại danh sách để kiểm tra trước khi thử lại.");
            }
        } catch (error) {
            window.showToast(error.message || "Không thể lưu dữ liệu.", "danger");
        } finally {
            delete form.dataset.submitting;
            setSubmitLoading(form, false);
        }
    });
})();
