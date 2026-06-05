 
    const authCard = document.getElementById("authCard");

    function showSignup() {
        authCard.classList.add("switching");

            setTimeout(() => {
                authCard.classList.add("signup-mode");
                authCard.classList.remove("switching");
            }, 200);
        }

        function showLogin() {
            authCard.classList.add("switching");

            setTimeout(() => {
                authCard.classList.remove("signup-mode");
                authCard.classList.remove("switching");
            }, 200);
        }
//xu ly form login


document.getElementById("loginForm").addEventListener("submit", async function (e) {
    e.preventDefault();

    const data = {
        username: document.getElementById("username").value,
        password: document.getElementById("password").value
    };

    const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(data)
    });

    const result = await response.json();

    if (response.ok) {
        console.log("Đăng nhập thành công", result);

        // ví dụ lưu token
        localStorage.setItem("accessToken", result.accessToken);

        // chuyển trang
        window.location.href = "/Chat";
    } else {
        alert(result.message || "Đăng nhập thất bại");
    }
});

// xử lý form signup
document.getElementById("signupForm").addEventListener("submit", async function (e) {
    e.preventDefault();

    const data = {
        firstname: document.getElementById("firstnameSignUp").value,
        lastname: document.getElementById("lastnameSignUp").value,
        username: document.getElementById("usernameSignUp").value,
        email: document.getElementById("emailSignUp").value,
        password: document.getElementById("passwordSignUp").value
        
    };

    const response = await fetch("/api/auth/signup", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(data)
    });

    const result = await response.json();

    if (response.ok) {
        console.log("Tạo tài khoản thành công", result);

       
        // chuyển trang
        window.location.href = "/";
    } else {
        alert(result.message || "Tạo tài khoản thất bại");
    }
});

// validate form
document.addEventListener("DOMContentLoaded", function () {
    const loginForm = document.getElementById("loginForm");
    const signupForm = document.getElementById("signupForm");

    function showError(input, message) {
        const inputBox = input.closest(".input-box");
        const error = inputBox.querySelector(".error-message");

        inputBox.classList.add("error");
        error.innerText = message;
    }

    function clearError(input) {
        const inputBox = input.closest(".input-box");
        const error = inputBox.querySelector(".error-message");

        inputBox.classList.remove("error");
        error.innerText = "";
    }

    function isEmpty(input) {
        return input.value.trim() === "";
    }

    loginForm.addEventListener("submit", function (e) {
        let valid = true;

        const username = document.getElementById("username");
        const password = document.getElementById("password");

        clearError(username);
        clearError(password);

        if (isEmpty(username)) {
            showError(username, "Vui lòng nhập username");
            valid = false;
        }

        if (isEmpty(password)) {
            showError(password, "Vui lòng nhập password");
            valid = false;
        }

        if (!valid) {
            e.preventDefault();
        }
    });
    
    // form sinup
    signupForm.addEventListener("submit", function (e) {
        let valid = true;

        const firstName = document.getElementById("firstnameSignUp");
        const lastName = document.getElementById("lastnameSignUp");
        const username = document.getElementById("usernameSignUp");
        const email = document.getElementById("emailSignUp");
        const password = document.getElementById("passwordSignUp");

        [firstName, lastName, username, email, password].forEach(clearError);

        if (isEmpty(firstName)) {
            showError(firstName, "Vui lòng nhập họ");
            valid = false;
        }

        if (isEmpty(lastName)) {
            showError(lastName, "Vui lòng nhập tên");
            valid = false;
        }

        if (isEmpty(username)) {
            showError(username, "Vui lòng nhập username");
            valid = false;
        }

        if (isEmpty(email)) {
            showError(email, "Vui lòng nhập email");
            valid = false;
        } else if (!email.value.includes("@")) {
            showError(email, "Email không hợp lệ");
            valid = false;
        }

        if (isEmpty(password)) {
            showError(password, "Vui lòng nhập password");
            valid = false;
        } else if (password.value.length < 6) {
            showError(password, "Password phải từ 6 ký tự");
            valid = false;
        }

        if (!valid) {
            e.preventDefault();
        }
    });
});

