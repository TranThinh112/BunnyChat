import { refreshAccessToken} from "./Api_Chat.js";

// Gắn accessToken vào các requets


export async function apiFetch(url, options = {}) {

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

    let response = await fetch(url, {
        ...options,
        headers,
        credentials: "include"
    });

// AccessToken hết hạn
    if (response.status === 403) {

        const newToken = await refreshAccessToken();

        if (!newToken) {

            localStorage.removeItem("accessToken");
            window.location.replace("/");

            return response;
        }

        // lưu accessToken mới
        localStorage.setItem(
            "accessToken",
            newToken
        );

        // gắn token mới
        headers["Authorization"] =
            `Bearer ${newToken}`;

        // gọi lại request cũ
        response = await fetch(url, {
            ...options,
            headers,
            credentials: "include"
        });
    }
    //gửi requets kèm header có chwwaas bearer kèm token
    return response;
}