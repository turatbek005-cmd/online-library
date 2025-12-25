const API_URL = "http://localhost:5283/api";

document.getElementById('registerForm').addEventListener('submit', async function(e) {
    e.preventDefault(); // Останавливаем стандартную перезагрузку страницы
    
    const username = document.getElementById('username').value;
    const email = document.getElementById('email').value;
    const password = document.getElementById('password').value;
    const confirm = document.getElementById('confirm').value;
    
    const btn = this.querySelector('button');
    const originalText = btn.innerText;

    // 1. Проверка паролей
    if (password !== confirm) {
        alert('Пароли не совпадают!');
        return;
    }

    // Блокируем кнопку и меняем текст
    btn.innerText = "Создание...";
    btn.disabled = true;

    try {
        // --- ШАГ 1: РЕГИСТРАЦИЯ ---
        const regResponse = await fetch(`${API_URL}/auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, email, password })
        });

        // Если регистрация не удалась (например, email занят)
        if (!regResponse.ok) {
            const errorData = await regResponse.json();
            alert(errorData.message || "Ошибка регистрации");
            btn.innerText = originalText;
            btn.disabled = false;
            return;
        }

        // --- ШАГ 2: АВТОМАТИЧЕСКИЙ ВХОД (LOGIN) ---
        btn.innerText = "Входим в аккаунт...";
        
        const loginResponse = await fetch(`${API_URL}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        if (loginResponse.ok) {
            const loginData = await loginResponse.json();
            
            // Сохраняем токен и данные
            localStorage.setItem('token', loginData.token);
            localStorage.setItem('user', JSON.stringify(loginData.user));

            // --- ШАГ 3: ПЕРЕНАПРАВЛЕНИЕ ---
            // Самый важный момент — переход на профиль
            window.location.href = 'profile.html';
        } else {
            // Если зарегистрировались, но не смогли войти (редкость)
            alert("Аккаунт создан! Теперь войдите вручную.");
            window.location.href = 'login.html';
        }

    } catch (error) {
        console.error("Ошибка:", error);
        alert('Ошибка соединения с сервером.');
        btn.innerText = originalText;
        btn.disabled = false;
    }
});