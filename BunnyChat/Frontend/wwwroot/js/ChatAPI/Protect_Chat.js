
import { refreshAccessToken } from "./Api_Chat_Refresh.js"
//protect route /chat bằng việc kiểm tra accestoken

//là hàm async vì bên trong có dùng await
async function protectChatRoute() {

    //lấy accestoken từ localstorege
    let token = localStorage.getItem("accessToken");

    //kiểm tra token có tồn tại ko. nếu ko có thì back /login
    if (!token || token.trim() === "") {
        localStorage.removeItem("accessToken");
        window.location.replace("/");
        return;
    }

    //block UI chat để ko bị chớp
    document.body.style.display = "block";

    // kiểm tra token còn hạn hay ko
    function isTokenExpired(token) {
        try {

            //tách payload và atob: giải mã. Chuyển json thành payload.exp
            const payload = JSON.parse(atob(token.split(".")[1]));

            // so sánh time. vì Date.now là milliseconds, payload.exp là seconds nên phải *1000 để bằng giá trị
            return Date.now() >= payload.exp * 1000;
        } catch {
            return true;
        }
    }

    // xử lý nếu AcessToken hết hạn khi reload or truy cập lại sau 1 time
    if (isTokenExpired(token)) {

        // gọi API để trả accesToken mới
        const newToken = await refreshAccessToken();

        //nếu RefreshToken trong session = null => xóa token cũ, đá về trang login
        if (!newToken) {

            localStorage.removeItem("accessToken");

            window.location.replace("/");

            return;
        }
        //  nếu refresh thành công, thì ghi đè token mới 
        localStorage.setItem(
            "accessToken",
            newToken
        );
        //cập nhật lại token, nếu ko có thì vẫn gửi lại AccessToken cũ 

        token = newToken;
    }

    try {
        // gọi api trả dữ liệu user, 

        const response = await fetch("/api/users/me", {
            method: "GET",
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });
        // const response = await apiFetch("/api/users/me");

        //nếu api res 401 or 403 thì xóa token, các TH: Session bị xóa => 401; JWT sai chữ ký => 401
        // kiểm tra phía server, vì server ko thể tự xóa local cuẩ browser
        if (response.status === 401 || response.status === 403) {
            localStorage.removeItem("accessToken");
            window.location.replace("/");
            return
        }

        ///phan hien thi thong tin cua /me

        //đọc dữ liệu trẩ về
        const result = await response.json();

        // hiển thị username
        document.getElementById("currentUsername").textContent =
            result.data.username || "UserName";

        //hiển thị displayname
        document.getElementById("currentDisplayname").textContent =
            result.data.displayName || "DisplayName";

        //bắt lỗi từ server
    } catch {
        localStorage.removeItem("accessToken");
        window.location.replace("/");
    }
}
protectChatRoute();
