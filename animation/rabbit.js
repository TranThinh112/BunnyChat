const password = document.getElementById("password");
const rabbit = document.getElementById("rabbit");

password.addEventListener("focus", () => {
    rabbit.classList.add("hide");
});

password.addEventListener("blur", () => {
    rabbit.classList.remove("hide");
});

document.addEventListener("mousemove",(e)=>{
   const eyes = document.querySelectorAll(".eye");
   eyes.forEach(eye=>{
      const x = (e.clientX/window.innerWidth - 0.5)*10;
      const y = (e.clientY/window.innerHeight - 0.5)*10;
      eye.style.transform = `translate(${x}px,${y}px)`;
   });
});