document.addEventListener("DOMContentLoaded", async () => {
    const token = sessionStorage.getItem("lms_token");
    if (!token) { window.location.href = "../pages/login.html"; return; }
    const msg = document.getElementById("managerDashboardMessage");
    const money = value => new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 0 }).format(value || 0);
    const date = value => value ? new Date(value).toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" }) : "—";
    try {
        const profile = await API.get("/api/auth/me");
        if (profile.user?.role !== "Manager") throw new Error("This page is available only to Manager accounts.");
        const data = await API.get("/api/manager/dashboard");
        document.getElementById("managerName").textContent = profile.user.name || "Branch Manager";
        document.getElementById("managerEmail").textContent = profile.user.email || "Manager Account";
        document.getElementById("managerAvatar").textContent = (profile.user.name || "M").trim().charAt(0).toUpperCase();
        document.getElementById("managerBranch").textContent = data.branch.name;
        document.getElementById("totalStudents").textContent = data.totalStudents;
        document.getElementById("activeTrainers").textContent = data.activeTrainers;
        document.getElementById("attendancePercent").textContent = `${data.attendancePercent}%`;
        document.getElementById("attendanceCaption").textContent = `${data.presentCount} present out of ${data.todayAttendanceCount} recorded`;
        document.getElementById("feeCollection").textContent = money(data.feeCollection);
        const present = data.todayAttendanceCount ? data.presentCount / data.todayAttendanceCount * 100 : 0;
        const absent = data.todayAttendanceCount ? (data.todayAttendanceCount - data.presentCount) / data.todayAttendanceCount * 100 : 0;
        document.getElementById("presentPercent").textContent = `${present.toFixed(1)}%`;
        document.getElementById("presentBar").style.width = `${present}%`;
        document.getElementById("absentPercent").textContent = `${absent.toFixed(1)}%`;
        document.getElementById("absentBar").style.width = `${absent}%`;
        document.getElementById("latePercent").textContent = "—";
        document.getElementById("lateBar").style.width = "0%";
        const tbody = document.getElementById("recentAdmissionsBody");
        tbody.replaceChildren();
        if (!data.recentAdmissions.length) {
            const row = document.createElement("tr"); const cell = document.createElement("td");
            cell.colSpan = 4; cell.textContent = "No admissions recorded for this branch yet."; row.appendChild(cell); tbody.appendChild(row);
        } else data.recentAdmissions.forEach(item => {
            const row = document.createElement("tr");
            [item.name, item.course, date(item.date), item.status].forEach(value => { const cell = document.createElement("td"); cell.textContent = value ?? "—"; row.appendChild(cell); });
            tbody.appendChild(row);
        });
        msg.textContent = `Live data loaded for ${data.branch.name}. Your access is restricted to this branch.`;
    } catch (error) {
        msg.textContent = error.message || "Unable to load branch dashboard.";
        if (/only to Manager|no assigned branch|assigned branch/i.test(msg.textContent)) msg.style.color = "#b42318";
    }
});
