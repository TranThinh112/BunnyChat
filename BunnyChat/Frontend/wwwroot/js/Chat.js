

// xử lý nút đăng xuất 
document.getElementById("logoutBtn").addEventListener("click", async function (e) {
    e.preventDefault();

    try{
        const response = await fetch("/api/auth/signout", {
             method: "POST",
        });

        if (response.ok) {
            // chuyển trang
            window.location.href = "/";
        } else {
            alert(result.message || "Đăng xuất thất bại");
        }
    } catch (error) {
        console.error(error);
        alert("Có lỗi xảy ra");
    }
});
