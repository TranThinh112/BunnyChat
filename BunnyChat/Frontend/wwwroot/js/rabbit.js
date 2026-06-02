const rabbit = document.getElementById("rabbit");
const passwords = document.querySelectorAll(".rabbit-password");

if (rabbit && passwords.length > 0) {
    passwords.forEach(password => {
        password.addEventListener("focus", () => {
            rabbit.classList.add("hide");
        });

        password.addEventListener("blur", () => {
            rabbit.classList.remove("hide");
        });
    });
}

const eyes = document.querySelectorAll(".rabbit .eye");

if (eyes.length > 0) {
    document.addEventListener("mousemove", (e) => {
        const x = (e.clientX / window.innerWidth - 0.5) * 10;
        const y = (e.clientY / window.innerHeight - 0.5) * 10;

        eyes.forEach(eye => {
            eye.style.transform = `translate(${x}px, ${y}px)`;
        });
    });
}