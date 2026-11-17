(() => {
  const token = sessionStorage.getItem("lms_token");
  const form = document.getElementById("changeForm");
  const message = document.getElementById("message");
  const button = document.getElementById("submitButton");
  if (!token) { location.href = "./login.html"; return; }
  const show = (text, type) => { message.textContent = text; message.className = `msg show ${type}`; };
  form.addEventListener("submit", async event => {
    event.preventDefault();
    const currentPassword = document.getElementById("currentPassword").value;
    const newPassword = document.getElementById("newPassword").value;
    const confirmPassword = document.getElementById("confirmPassword").value;
    if (newPassword.length < 12) return show("Use at least 12 characters for your new password.", "error");
    if (newPassword !== confirmPassword) return show("The new passwords do not match.", "error");
    button.disabled = true; button.textContent = "Updating…";
    try {
      const base = (window.LMS_CONFIG?.API_BASE_URL || "").replace(/\/$/, "");
      const response = await fetch(`${base}/api/auth/change-password`, { method: "POST", headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` }, body: JSON.stringify({ currentPassword, newPassword }) });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(data.message || `Request failed (${response.status})`);
      sessionStorage.removeItem("lms_token"); sessionStorage.removeItem("lms_user");
      show("Password updated. Please sign in with your new password.", "success");
      setTimeout(() => location.href = "./login.html", 1300);
    } catch (error) { show(error.message, "error"); }
    finally { button.disabled = false; button.textContent = "Update password"; }
  });
})();
