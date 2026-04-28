window.appLogout = function appLogout() {
    document.cookie = "Username=; Max-Age=0; path=/;";
    fetch("/clear-session", { method: "POST" })
        .then(() => {
            window.location.href = "/Login";
        });
};
