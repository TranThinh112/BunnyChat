 
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


