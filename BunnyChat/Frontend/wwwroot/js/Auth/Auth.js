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

const loginForm = document.getElementById("loginForm");
const signupForm = document.getElementById("signupForm");

const username = document.getElementById("username");
const password = document.getElementById("password");
const loginError = document.getElementById("loginError");
const signupError = document.getElementById("signupError");

const firstName = document.getElementById("firstName");
const lastName = document.getElementById("lastName");
const email = document.getElementById("email");

// validate form
document.addEventListener("DOMContentLoaded", function () {
    

    // biến hiện các lỗi thiếu input
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
    
    // biến hiện lỗi sai mật khẩu or username
     function showLoginError(message) {
            loginError.style.display = "block";
            loginError.textContent = message;
        }

        function clearLoginError() {
            loginError.style.display = "none";
            loginError.textContent = "";
        }
        //clear lỗi api trả về
     function showSginupError(message) {
            signupError.style.display = "block";
            signupError.textContent = message;
        }

        function clearSignupError() {
            signupError.style.display = "none";
            signupError.textContent = "";
        }

         function clearAllErrors() {
            clearError(username);
            clearError(password);
            clearLoginError();
            clearSignupError
        }

        // clear lỗi thiếu input khi bắt đầu nhập
        username.addEventListener("input", () => {
            clearError(username);
            clearLoginError();
        });

        password.addEventListener("input", () => {
            clearError(password);
            clearLoginError();
        });


    //xử lysa form login
    loginForm.addEventListener("submit", async function (e) {
        e.preventDefault();

        let isValid = true;

        clearAllErrors();


        if (username.value.trim() === "") {
            showError(username, "Vui lòng nhập username");
            isValid = false;
        }

        if (password.value.trim() === "") {
            showError(password, "Vui lòng nhập password");
            isValid = false;
        }

        // Chặn gọi API nếu chưa nhập
        if (!isValid) return;
       
        try{
            const response = await login(username, password)
            const result = await response.json();

            if (response.ok) {
                window.location.href = "/Chat";
            } else {
                showLoginError(
                    result.message || "Username hoặc Password không chính xác"
                );
            }
        }catch (error) {
                console.error(error);
                showLoginError("Không thể kết nối đến server");
        }
    });

    
    // form sinup
   signupForm.addEventListener("submit", async function (e) {
        e.preventDefault();
    
        let valid = true;

        clearAllErrors();
        if (firstName.value.trim() === "") {
            showError(firstName, "Vui lòng nhập họ");
            valid = false;
        }

        if (lastName.value.trim() === "") {
            showError(lastName, "Vui lòng nhập tên");
            valid = false;
        }

        if (username.value.trim() === "") {
            showError(username, "Vui lòng nhập username");
            isValid = false;
        }


        if (email.value.trim() === "") {
            showError(email, "Vui lòng nhập email");
            valid = false;
        } else if (!email.value.includes("@")) {
            showError(email, "Email không hợp lệ");
            valid = false;
        }

        if (password.value.trim() === "") {
            showError(password, "Vui lòng nhập password");
            isValid = false;
        } else if (password.value.length < 6) {
            showError(password, "Password phải từ 6 ký tự");
            valid = false;
        }

         // Chặn gọi API nếu chưa nhập
        if (!isValid) return;
        
        try{
                const response = await signup(firstName,lastName, email, username, password)
                const result = await response.json();

                if (response.ok) {
                    window.location.href = "/";
                } else {
                    showLoginError(
                        result.message || "Username hoặc Email đã tồn tại"
                    );
                }
            }catch (error) {
                    console.error(error);
                    showLoginError("Không thể kết nối đến server");
            }
    });
});


