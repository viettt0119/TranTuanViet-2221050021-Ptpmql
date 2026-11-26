// Nút hiển thị/che mật khẩu cho mọi ô <input type="password">.
// Bỏ qua ô nào có data-password-toggle="off". Gọi lại window.initPasswordToggles(root) cho nội dung nạp bằng Ajax.
(() => {
    const SHOW_LABEL = "Hiển thị mật khẩu";
    const HIDE_LABEL = "Che mật khẩu";

    function enhance(input) {
        if (input.dataset.passwordToggleReady === "true" || input.dataset.passwordToggle === "off") return;
        input.dataset.passwordToggleReady = "true";

        const wrapper = document.createElement("div");
        wrapper.className = "password-field";
        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);

        const button = document.createElement("button");
        button.type = "button";
        button.className = "password-toggle";
        button.setAttribute("aria-label", SHOW_LABEL);
        button.setAttribute("aria-pressed", "false");
        button.title = SHOW_LABEL;
        if (input.id) button.setAttribute("aria-controls", input.id);
        button.innerHTML = '<i class="bi bi-eye" aria-hidden="true"></i>';
        wrapper.appendChild(button);

        const capsHint = document.createElement("div");
        capsHint.className = "password-caps-hint";
        capsHint.setAttribute("role", "status");
        capsHint.innerHTML = '<i class="bi bi-capslock-fill me-1" aria-hidden="true"></i>Caps Lock đang bật';
        wrapper.insertAdjacentElement("afterend", capsHint);

        button.addEventListener("click", () => {
            const reveal = input.type === "password";
            // Giữ vị trí con trỏ khi đổi kiểu ô nhập.
            const start = input.selectionStart;
            const end = input.selectionEnd;
            input.type = reveal ? "text" : "password";
            button.setAttribute("aria-pressed", String(reveal));
            button.setAttribute("aria-label", reveal ? HIDE_LABEL : SHOW_LABEL);
            button.title = reveal ? HIDE_LABEL : SHOW_LABEL;
            button.innerHTML = `<i class="bi ${reveal ? "bi-eye-slash" : "bi-eye"}" aria-hidden="true"></i>`;
            input.focus();
            if (start !== null && end !== null) {
                try { input.setSelectionRange(start, end); } catch (_) { /* một số trình duyệt không hỗ trợ */ }
            }
        });

        const updateCaps = (event) => {
            if (typeof event.getModifierState !== "function") return;
            capsHint.classList.toggle("show", event.getModifierState("CapsLock"));
        };
        input.addEventListener("keydown", updateCaps);
        input.addEventListener("keyup", updateCaps);
        input.addEventListener("blur", () => capsHint.classList.remove("show"));
    }

    // Không để lộ mật khẩu đang hiển thị khi gửi form (trình duyệt có thể lưu vào lịch sử ô text).
    document.addEventListener("submit", (event) => {
        event.target.querySelectorAll?.('input[data-password-toggle-ready="true"][type="text"]').forEach(input => {
            input.type = "password";
            const button = input.parentElement?.querySelector(".password-toggle");
            if (button) {
                button.setAttribute("aria-pressed", "false");
                button.setAttribute("aria-label", SHOW_LABEL);
                button.title = SHOW_LABEL;
                button.innerHTML = '<i class="bi bi-eye" aria-hidden="true"></i>';
            }
        });
    }, true);

    window.initPasswordToggles = (root = document) => {
        root.querySelectorAll('input[type="password"]').forEach(enhance);
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => window.initPasswordToggles());
    } else {
        window.initPasswordToggles();
    }
})();
