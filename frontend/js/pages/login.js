    const API_URL = "http://localhost:5283/api";

    document.getElementById('loginForm').addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const email = document.getElementById('email').value;
        const password = document.getElementById('password').value;
        const btn = this.querySelector('button');
        const originalText = btn.innerText;
        
        btn.innerText = "Проверка...";

        try {
            const response = await fetch(`${API_URL}/auth/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password })
            });

            const data = await response.json();

            if (response.ok) {
                localStorage.setItem('token', data.token);
                localStorage.setItem('user', JSON.stringify(data.user));
                showModal('Успешно!', 'Добро пожаловать домой, читатель.', true);
                setTimeout(() => { window.location.href = 'profile.html'; }, 1500);
            } else {
                showModal('Ошибка входа', data.message || "Неверный email или пароль", false);
            }
        } catch (error) {
            showModal('Ошибка сети', 'Сервер библиотеки не отвечает.', false);
        } finally {
            btn.innerText = originalText;
        }
    });

    function showModal(title, message, isSuccess) {
        const modal = document.getElementById('customModal');
        document.getElementById('modalTitle').innerText = title;
        document.getElementById('modalMessage').innerText = message;
        document.getElementById('modalIcon').innerText = isSuccess ? '✨' : '🛑';
        modal.style.display = 'flex';
    }

    function closeModal() {
        document.getElementById('customModal').style.display = 'none';
    }