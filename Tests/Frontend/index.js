const paragraph = document.querySelector("p");
const select = document.querySelector("select");
const button = document.querySelector("button");

select.addEventListener("change", () => {
    paragraph.textContent = "";
});

button.addEventListener("click", async () => {
    try {
        let response = await fetch(`${select.value}/api/User/Login`,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },  
                body: JSON.stringify({
                    "email": "admin@chatfish.be",
                    "password": "admin123"
                })
            }
        );
        if (response.ok) {
            response = await fetch(`${select.value}/api/StoryMessage`, {
                credentials: "include"
            });
            if (response.ok) {
                const data = await response.json();
                paragraph.textContent = "Login successful! Story messages: " + JSON.stringify(data);
            } else {
                paragraph.textContent = "Failed to fetch story messages: " + response.statusText;
            }
        } else {
            paragraph.textContent = "Login failed: " + response.statusText;
        }
    } catch (ex) {
        paragraph.textContent = "An unexpected error occurred: " + ex.message;
    }
});