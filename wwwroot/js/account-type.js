// Đổi nhãn / gợi ý ô mã theo tư cách; kiểm tra định dạng thật sự nằm ở server (RegisterViewModel.Validate).
(() => {
    const form = document.getElementById("register-form");
    if (!form) return;
    const label = document.getElementById("member-code-label");
    const icon = document.getElementById("member-code-icon");
    const input = document.getElementById("MemberCode");
    const note = document.getElementById("staff-approval-note");
    const apply = (isStaff) => {
        label.textContent = isStaff ? "Mã cán bộ" : "Mã sinh viên";
        input.placeholder = isStaff ? "VD: GV0123" : "VD: 2521050001 (10 số, bắt đầu bằng 2)";
        input.inputMode = isStaff ? "text" : "numeric";
        icon.className = "bi " + (isStaff ? "bi-person-vcard" : "bi-person-badge");
        note.hidden = !isStaff;
    };
    form.addEventListener("change", (event) => {
        if (event.target.name === "AccountType") apply(event.target.value === "Staff");
    });
})();
