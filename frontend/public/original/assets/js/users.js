(() => {
  "use strict";
  const API_BASE = (window.LMS_CONFIG && window.LMS_CONFIG.API_BASE_URL || "").replace(/\/$/, "");
  const token = sessionStorage.getItem("lms_token");
  const $ = (id) => document.getElementById(id);
  const state = { users: [], branches: [], editingId: null };

  if (!token) {
    showNotice("Your session has expired. Please sign in again.", "error");
    setTimeout(() => location.href = "../pages/login.html", 1000);
    return;
  }

  async function request(path, options = {}) {
    const response = await fetch(`${API_BASE}${path}`, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${token}`,
        ...(options.headers || {})
      }
    });
    const body = response.status === 204 ? null : await response.json().catch(() => ({}));
    if (response.status === 401) {
      sessionStorage.removeItem("lms_token");
      location.href = "../pages/login.html";
      throw new Error("Please sign in again.");
    }
    if (!response.ok) throw new Error(body?.message || body?.error || `Request failed (${response.status}).`);
    return body;
  }

  function showNotice(message, type = "success") {
    const box = $("notice");
    box.textContent = message;
    box.className = `notice ${type}`;
    if (type === "success") setTimeout(() => { box.className = "notice"; }, 4500);
  }

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, ch => ({
      "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[ch]);
  }

  function showTemporaryPassword(user, password) {
    const safeEmail = escapeHtml(user.email || "");
    const safeName = escapeHtml(user.fullName || "user");
    const box = $("notice");
    box.innerHTML = `<strong>Temporary password for ${safeName}</strong><br>Email: ${safeEmail}<br><code id="oneTimePassword" style="font-size:15px;display:inline-block;margin:8px 0;padding:8px;background:#fff;border-radius:7px;color:#111">${escapeHtml(password)}</code><br><button type="button" class="mini" id="copyTempPassword">Copy password</button><br><span>Shown once only. Share it privately. The user must change it after signing in.</span>`;
    box.className = "notice success";
    $("copyTempPassword").addEventListener("click", async () => {
      try { await navigator.clipboard.writeText(password); $("copyTempPassword").textContent = "Copied"; }
      catch { showNotice("Clipboard access unavailable. Select and copy the password manually.", "error"); }
    });
  }

  async function loadAll() {
    $("userRows").innerHTML = '<tr><td colspan="7" class="empty">Loading accounts…</td></tr>';
    try {
      const [users, branches] = await Promise.all([
        request("/api/users"),
        request("/api/branches")
      ]);
      state.users = users;
      state.branches = branches;
      fillBranchOptions();
      render();
    } catch (error) {
      $("userRows").innerHTML = `<tr><td colspan="7" class="empty">${escapeHtml(error.message)}</td></tr>`;
      showNotice(error.message, "error");
    }
  }

  function fillBranchOptions() {
    const options = state.branches.map(b =>
      `<option value="${b.id}">${escapeHtml(b.name)} (${escapeHtml(b.code)})</option>`
    ).join("");
    $("branchFilter").innerHTML = '<option value="">All branches</option>' + options;
    $("branchId").innerHTML = '<option value="">No branch assigned</option>' + options;
  }

  function render() {
    const q = $("search").value.trim().toLowerCase();
    const role = $("roleFilter").value;
    const branch = $("branchFilter").value;
    const users = state.users.filter(u =>
      (!q || u.fullName.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)) &&
      (!role || u.role === role) &&
      (!branch || String(u.branchId ?? "") === branch)
    );
    $("totalCount").textContent = state.users.length;
    $("managerCount").textContent = state.users.filter(u => u.role === "Manager").length;
    $("trainerCount").textContent = state.users.filter(u => u.role === "Trainer").length;
    $("studentCount").textContent = state.users.filter(u => u.role === "Student").length;

    if (!users.length) {
      $("userRows").innerHTML = '<tr><td colspan="7" class="empty">No matching accounts found.</td></tr>';
      return;
    }
    $("userRows").innerHTML = users.map(u => {
      const branchName = state.branches.find(b => b.id === u.branchId)?.name || "Not assigned";
      const date = u.createdAt ? new Date(u.createdAt).toLocaleDateString() : "—";
      return `<tr>
        <td><span class="name">${escapeHtml(u.fullName)}</span><span class="sub">${escapeHtml(u.email)}</span></td>
        <td><span class="pill">${escapeHtml(u.role)}</span></td>
        <td>${escapeHtml(branchName)}</td>
        <td><span class="pill ${u.isActive ? "active" : "inactive"}">${u.isActive ? "Active" : "Inactive"}</span>${u.mustChangePassword ? '<span class="sub">Password change required</span>' : ''}${u.lockoutEndUtc && new Date(u.lockoutEndUtc)>new Date() ? '<span class="sub">Temporarily locked</span>' : ''}</td>
        <td>${u.lastLoginAt ? escapeHtml(new Date(u.lastLoginAt).toLocaleString()) : "Never"}</td>
        <td>${escapeHtml(date)}</td>
        <td><div class="actions">
          <button class="mini" data-edit="${u.id}">Edit</button>
          <button class="mini" data-reset="${u.id}">Reset password</button>
          <button class="mini" data-toggle="${u.id}" data-active="${u.isActive}">${u.isActive ? "Deactivate" : "Activate"}</button>
        </div></td></tr>`;
    }).join("");
  }

  function openModal(user = null) {
    state.editingId = user?.id ?? null;
    $("modalTitle").textContent = user ? "Edit user" : "Create user";
    $("saveUser").textContent = user ? "Save changes" : "Create account";
    $("fullName").value = user?.fullName || "";
    $("email").value = user?.email || "";
    $("role").value = user?.role || "Manager";
    $("branchId").value = user?.branchId ?? "";
    $("userModal").classList.add("open");
  }
  function closeModal() { $("userModal").classList.remove("open"); $("userForm").reset(); }

  $("addUser").addEventListener("click", () => openModal());
  $("closeModal").addEventListener("click", closeModal);
  $("cancel").addEventListener("click", closeModal);
  $("userModal").addEventListener("click", e => { if (e.target === $("userModal")) closeModal(); });
  $("refresh").addEventListener("click", loadAll);
  ["search", "roleFilter", "branchFilter"].forEach(id => $(id).addEventListener(id === "search" ? "input" : "change", render));

  $("userRows").addEventListener("click", async e => {
    const editId = e.target.dataset.edit;
    const toggleId = e.target.dataset.toggle;
    const resetId = e.target.dataset.reset;
    if (editId) {
      const user = state.users.find(u => u.id === Number(editId));
      if (user) openModal(user);
    }
    if (resetId) {
      const user = state.users.find(u => u.id === Number(resetId));
      if (!user || !confirm(`Generate a new temporary password for ${user.fullName}? Their current password will stop working.`)) return;
      try {
        const result = await request(`/api/users/${user.id}/reset-password`, { method: "POST" });
        showTemporaryPassword(user, result.temporaryPassword);
        await loadAll();
      } catch (error) { showNotice(error.message, "error"); }
    }
    if (toggleId) {
      const user = state.users.find(u => u.id === Number(toggleId));
      if (!user) return;
      const next = !user.isActive;
      if (!confirm(`${next ? "Activate" : "Deactivate"} ${user.fullName}'s account?`)) return;
      try {
        await request(`/api/users/${user.id}/status`, {
          method: "PATCH", body: JSON.stringify({ isActive: next })
        });
        showNotice(`Account ${next ? "activated" : "deactivated"} successfully.`);
        await loadAll();
      } catch (error) { showNotice(error.message, "error"); }
    }
  });

  $("userForm").addEventListener("submit", async e => {
    e.preventDefault();
    const payload = {
      fullName: $("fullName").value.trim(),
      email: $("email").value.trim(),
      role: $("role").value,
      branchId: $("branchId").value ? Number($("branchId").value) : null
    };
    const editing = state.editingId !== null;
    $("saveUser").disabled = true;
    try {
      const result = await request(editing ? `/api/users/${state.editingId}` : "/api/users", {
        method: editing ? "PUT" : "POST",
        body: JSON.stringify(payload)
      });
      closeModal();
      if (!editing && result.temporaryPassword) showTemporaryPassword(result, result.temporaryPassword);
      else showNotice("User details updated.");
      await loadAll();
    } catch (error) {
      showNotice(error.message, "error");
    } finally {
      $("saveUser").disabled = false;
    }
  });

  loadAll();
})();