import { login, signup } from "./API_Auth.js";


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


window.showSignup = showSignup;
window.showLogin = showLogin;

document.addEventListener("DOMContentLoaded", function () {
    const loginForm = document.getElementById("loginForm");
    const signupForm = document.getElementById("signupForm");

    const loginUsername = document.getElementById("username");
    const loginPassword = document.getElementById("password");
    const loginError = document.getElementById("loginError");

    const firstName = document.getElementById("firstnameSignUp");
    const lastName = document.getElementById("lastnameSignUp");
    const signupUsername = document.getElementById("usernameSignUp");
    const email = document.getElementById("emailSignUp");
    const signupPassword = document.getElementById("passwordSignUp");
    const signupError = document.getElementById("signupError");

    function showError(input, message) {
        const inputBox = input.closest(".input-box");
        const error = inputBox.querySelector(".error-message");

        inputBox.classList.add("error");
        error.textContent = message;
    }

    function clearError(input) {
        const inputBox = input.closest(".input-box");
        const error = inputBox.querySelector(".error-message");

        inputBox.classList.remove("error");
        error.textContent = "";
    }

    function showFormError(errorElement, message) {
        errorElement.style.display = "block";
        errorElement.textContent = message;
    }

    function clearFormError(errorElement) {
        errorElement.style.display = "none";
        errorElement.textContent = "";
    }

    function clearLoginErrors() {
        clearError(loginUsername);
        clearError(loginPassword);
        clearFormError(loginError);
    }

    function clearSignupErrors() {
        clearError(firstName);
        clearError(lastName);
        clearError(signupUsername);
        clearError(email);
        clearError(signupPassword);
        clearFormError(signupError);
    }

    loginUsername.addEventListener("input", clearLoginErrors);
    loginPassword.addEventListener("input", clearLoginErrors);

    firstName.addEventListener("input", clearSignupErrors);
    lastName.addEventListener("input", clearSignupErrors);
    signupUsername.addEventListener("input", clearSignupErrors);
    email.addEventListener("input", clearSignupErrors);
    signupPassword.addEventListener("input", clearSignupErrors);

    loginForm.addEventListener("submit", async function (e) {
        e.preventDefault();

        let isValid = true;
        clearLoginErrors();

        if (loginUsername.value.trim() === "") {
            showError(loginUsername, "Vui lòng nhập username");
            isValid = false;
        }

        if (loginPassword.value.trim() === "") {
            showError(loginPassword, "Vui lòng nhập password");
            isValid = false;
        }

        if (!isValid) return;

        try {
            const response = await login(loginUsername, loginPassword);
            const result = await response.json();

            if (response.ok) {
                localStorage.setItem("accessToken", result.data.accessToken);
                window.location.href = "/Chat";
            } else {
                showFormError(loginError, result.message || "Sai tài khoản hoặc mật khẩu");
            }
        } catch (error) {
            console.error(error);
            showFormError(loginError, "Không thể kết nối đến server");
        }
    });

    signupForm.addEventListener("submit", async function (e) {
        e.preventDefault();

        let isValid = true;
        clearSignupErrors();

        if (firstName.value.trim() === "") {
            showError(firstName, "Vui lòng nhập họ");
            isValid = false;
        }

        if (lastName.value.trim() === "") {
            showError(lastName, "Vui lòng nhập tên");
            isValid = false;
        }

        if (signupUsername.value.trim() === "") {
            showError(signupUsername, "Vui lòng nhập username");
            isValid = false;
        }

        if (email.value.trim() === "") {
            showError(email, "Vui lòng nhập email");
            isValid = false;
        } else if (!email.value.includes("@")) {
            showError(email, "Email không hợp lệ");
            isValid = false;
        }

        if (signupPassword.value.trim() === "") {
            showError(signupPassword, "Vui lòng nhập password");
            isValid = false;
        } else if (signupPassword.value.length < 6) {
            showError(signupPassword, "Password phải từ 6 ký tự");
            isValid = false;
        }

        if (!isValid) return;

        try {
            const response = await signup(firstName, lastName, email, signupUsername, signupPassword);
            const result = await response.json();

            if (response.ok) {
                window.location.href = "/";
            } else {
                showFormError(signupError, result.message || "Username hoặc Email đã tồn tại");
            }
        } catch (error) {
            console.error(error);
            showFormError(signupError, "Không thể kết nối đến server");
        }
    });
});