
async function logoutUser(button = null) {
    const originalText = button?.textContent;

    try {
        if (button) {
            button.disabled = true;
            button.textContent = "Logging out...";
        }

        await API.post("/api/logout", {});

        // Clear temporary browser session information.
        sessionStorage.clear();

        window.location.replace("../pages/login.html");

    } catch (error) {
        console.error("Logout failed:", error.message);

        if (button) {
            button.disabled = false;
            button.textContent = originalText || "Logout";
        }

        alert("Unable to log out. Please try again.");
    }
}

document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-logout]").forEach(button => {
        button.addEventListener("click", () => logoutUser(button));
    });
});
