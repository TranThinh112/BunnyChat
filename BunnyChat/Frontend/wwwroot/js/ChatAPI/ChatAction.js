// xử lý logic các Action của user

//xử lý nút search, trả về responese trong console
document.getElementById("searchBtn")
    .addEventListener("click", async () => {

        //lấy giá trị input
        const keyword = document.getElementById("keyword").value;

        //gọi api /search, encodeURIComponent: mã hóa ký tự tiếng Việt và dấu cách.
        const res = await apiFetch(`/api/users/search?q=${encodeURIComponent(keyword)}`);

        //đọc dữ liệu json từ res rồi gán vào biến result
        const result = await res.json();

        //log ra console
        console.log(res.status);
        console.log(result);
        
    });


// xử lý nút đăng xuất 
document.getElementById("logoutBtn").addEventListener("click", async function (e) {
    e.preventDefault();

    try {
        // gọi api
        await fetch("/api/auth/signout", {
            method: "POST"
        });
    } finally {
        //xóa accessToken trong local và chuyển trang Auth
        localStorage.removeItem("accessToken");
        window.location.href = "/";
    }
});
