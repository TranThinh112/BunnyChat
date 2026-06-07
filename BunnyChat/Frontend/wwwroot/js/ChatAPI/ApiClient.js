// Gắn accessToken vào các requets

async function apiFetch(url, options = {}) {

    //lấy accestoken từ localStorage
    const token = localStorage.getItem("accessToken");

    // Tạo object headers Toán tử ... là spread operator. gắn cho headers
    const headers = {
        ...(options.headers || {})
    };

    // gắn Authorization, nếu tồn tại token gắn vào bearer
    if (token) {
        headers["Authorization"] = `Bearer ${token}`;
    }
    //gửi requets kèm header có chwwaas bearer kèm token
    return fetch(url, {
        ...options,
        headers
    })
}