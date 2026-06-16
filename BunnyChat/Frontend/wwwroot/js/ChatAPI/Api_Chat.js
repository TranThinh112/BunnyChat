//gọi API /refresh
export async function refreshAccessToken() {

    const response = await fetch(
        "/api/auth/refresh",
        {
            method: "POST",
            //gửi refreshToken lên server
            credentials: "include"
        }
    );

    if (!response.ok) {
        return null;
    }

    const result = await response.json();

    return result.data.accessToken;
}
