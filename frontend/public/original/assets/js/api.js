const API = (() => {
    const BASE_URL = (window.LMS_CONFIG && window.LMS_CONFIG.API_BASE_URL || "").replace(/\/$/, "");
    async function request(endpoint, options = {}) {
        const token = sessionStorage.getItem("lms_token");
        const headers = {
            ...(options.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
            ...(token ? { "Authorization": `Bearer ${token}` } : {}),
            ...options.headers
        };
        const response = await fetch(`${BASE_URL}${endpoint}`, { ...options, headers });
        const type = response.headers.get("content-type") || "";
        const data = response.status === 204 ? null : type.includes("application/json") ? await response.json() : await response.text();
        if (response.status === 401) { sessionStorage.removeItem("lms_token"); }
        if (!response.ok) throw new Error(data?.message || data?.error || `Request failed (${response.status}).`);
        return data;
    }
    return {
        get: endpoint => request(endpoint, { method: "GET" }),
        post: (endpoint, body) => request(endpoint, { method: "POST", body: body instanceof FormData ? body : JSON.stringify(body) }),
        put: (endpoint, body) => request(endpoint, { method: "PUT", body: JSON.stringify(body) }),
        patch: (endpoint, body) => request(endpoint, { method: "PATCH", body: JSON.stringify(body) }),
        delete: endpoint => request(endpoint, { method: "DELETE" })
    };
})();
