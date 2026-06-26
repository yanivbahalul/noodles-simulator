window.appLogout = function appLogout() {
    fetch("/clear-session", { method: "POST", credentials: "same-origin" })
        .catch(() => fetch("/Logout", { method: "POST", credentials: "same-origin" }))
        .finally(() => {
            window.location.href = "/Login";
        });
};
