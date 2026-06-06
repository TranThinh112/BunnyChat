export async function login(usernameInput, passwordInput) {

    const data = {
        username: usernameInput.value.trim(),
        password: passwordInput.value.trim()
    };

    const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(data)
    });

    return response;
}

export async function signup(firstnameInput, lastNameInput, emailInput, usernameInput, passwordInput) {

    const data = {
        firstName: firstnameInput.value.trim(),
        lastName: lastNameInput.value.trim(),
        email:  emailInput.value.trim(),
        username: usernameInput.value.trim(),
        password: passwordInput.value.trim()
    };

    const response = await fetch("/api/auth/signup", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(data)
    });

    return response;
}