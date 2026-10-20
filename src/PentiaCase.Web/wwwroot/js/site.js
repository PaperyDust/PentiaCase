document.querySelectorAll("form[data-refresh-data]").forEach(form => {
    form.addEventListener("submit", () => {
        const button = form.querySelector('button[type="submit"]');
        button.disabled = true;
        button.textContent = "Refreshing...";
        form.setAttribute("aria-busy", "true");
    });
});
