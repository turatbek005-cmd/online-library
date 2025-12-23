const API_URL = "http://localhost:5283/api";

    document.getElementById('registerForm').addEventListener('submit', async function(e) {
        e.preventDefault();

        const username = document.getElementById('username').value;
        const email = document.getElementById('email').value;
        const password = document.getElementById('password').value;
        const confirmPass = document.getElementById('confirm').value;

        if (password !== confirmPass) {
            showModal('Внимание', 'Пароли не совпадают!', false);
            return;
        }

        try {
            const response = await fetch(`${API_URL}/auth/register`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, email, password })
            });

            let data;
            const contentType = response.headers.get("content-type");
            if (contentType && contentType.indexOf("application/json") !== -1) {
                data = await response.json();
            } else {
                data = { message: await response.text() };
            }

            if (response.ok) {
                showModal('Добро пожаловать!', 'Аккаунт создан. Войдите в него.', true);
                const okBtn = document.querySelector('#customModal button');
                okBtn.onclick = function() { window.location.href = 'login.html'; };
            } else {
                showModal('Ошибка', data.message || "Не удалось создать аккаунт", false);
            }
        } catch (error) {
            showModal('Ошибка', 'Нет соединения с сервером', false);
        }
    });

    function showModal(title, message, isSuccess) {
        const modal = document.getElementById('customModal');
        document.getElementById('modalTitle').innerText = title;
        document.getElementById('modalMessage').innerText = message;
        document.getElementById('modalIcon').innerText = isSuccess ? '🎉' : '🛑';
        modal.style.display = 'flex';
    }

    function closeModal() {
        document.getElementById('customModal').style.display = 'none';
    }