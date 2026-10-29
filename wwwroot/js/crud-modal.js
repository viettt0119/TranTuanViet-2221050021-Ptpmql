(() => {
    const host = document.getElementById("crud-modal-host");
    const notification = document.getElementById("crud-modal-notification");

    if (!host || !notification) {
        return;
    }

    const showError = (message) => {
        notification.textContent = message;
        notification.classList.remove("alert-success");
        notification.classList.add("alert-danger");
        notification.classList.remove("d-none");
    };

    const displayModal = (html) => {
        const installModal = () => {
            host.innerHTML = html;
            const modalElement = host.querySelector(".modal");
            if (!modalElement) {
                throw new Error("Nội dung trả về không có hộp thoại hợp lệ.");
            }

            bootstrap.Modal.getOrCreateInstance(modalElement).show();
        };
        const currentModal = host.querySelector(".modal.show");
        if (!currentModal) {
            installModal();
            return;
        }

        currentModal.addEventListener("hidden.bs.modal", installModal, { once: true });
        bootstrap.Modal.getOrCreateInstance(currentModal).hide();
    };

    document.addEventListener("click", async (event) => {
        const trigger = event.target.closest("[data-modal-url]");
        if (!trigger) {
            return;
        }

        event.preventDefault();
        notification.classList.add("d-none");

        try {
            const response = await fetch(trigger.dataset.modalUrl, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) {
                throw new Error(`Không thể tải dữ liệu (HTTP ${response.status}).`);
            }

            displayModal(await response.text());
        } catch (error) {
            showError(error.message || "Không thể tải dữ liệu. Vui lòng thử lại.");
        }
    });

    host.addEventListener("submit", async (event) => {
        const form = event.target.closest("[data-ajax-modal-form]");
        if (!form) {
            return;
        }

        event.preventDefault();
        notification.classList.add("d-none");

        const submitButton = form.querySelector('[type="submit"]');
        if (submitButton) {
            submitButton.disabled = true;
        }

        try {
            const response = await fetch(form.action, {
                method: form.method || "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const contentType = response.headers.get("content-type") || "";

            if (contentType.includes("application/json")) {
                const result = await response.json();
                if (!response.ok || !result.success) {
                    throw new Error(result.message || `Yêu cầu thất bại (HTTP ${response.status}).`);
                }

                notification.textContent = result.message || "Đã lưu thay đổi.";
                notification.classList.remove("d-none", "alert-danger");
                notification.classList.add("alert-success");
                const modalElement = host.querySelector(".modal.show");
                if (!modalElement) {
                    window.location.reload();
                    return;
                }

                modalElement.addEventListener("hidden.bs.modal", () => {
                    window.setTimeout(() => window.location.reload(), 1200);
                }, { once: true });
                bootstrap.Modal.getOrCreateInstance(modalElement).hide();
                return;
            }

            const html = await response.text();
            if (!response.ok && response.status !== 422) {
                throw new Error(`Không thể lưu dữ liệu (HTTP ${response.status}).`);
            }

            displayModal(html);
        } catch (error) {
            showError(error.message || "Không thể lưu dữ liệu. Vui lòng thử lại.");
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    });

    host.addEventListener("hidden.bs.modal", (event) => {
        event.target.remove();
    });
})();
