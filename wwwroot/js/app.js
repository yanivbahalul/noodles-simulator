window.appLogout = function appLogout() {
    document.cookie = "Username=; Max-Age=0; path=/;";
    fetch("/clear-session", { method: "POST", credentials: "same-origin" })
        .catch(() => fetch("/Logout", { method: "POST", credentials: "same-origin" }))
        .finally(() => {
            window.location.href = "/Login";
        });
};
