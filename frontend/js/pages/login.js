const API_URL = "http://localhost:5283/api";

document.getElementById('loginForm').addEventListener('submit', async function(e) {
    e.preventDefault();
    
    const email = document.getElementById('email').value;
    const password = document.getElementById('password').value;
    const btn = this.querySelector('button');
    const originalText = btn.innerText;
    
    btn.innerText = "Проверка...";
    btn.disabled = true;

    try {
        const response = await fetch(`${API_URL}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        let data;
        try {
            data = await response.json();
        } catch (err) {
            data = { message: "Ошибка сервера (некорректный ответ)" };
        }

        if (response.ok) {
            // УСПЕХ
            localStorage.setItem('token', data.token);
            localStorage.setItem('user', JSON.stringify(data.user));
            
            // Исправлено: добавлен плюс для конкатенации строк
            const message = data.loginReward ? `${data.loginReward} Добро пожаловать домой, читатель.` : "Добро пожаловать домой, читатель.";
            showModal('Успешно!', message, true);
            
            setTimeout(() => { 
                window.location.href = 'index.html'; 
            }, 2000);
        } else {
            // ОШИБКА
            // Исправлено: использование оператора || вместо |
            showModal('Ошибка входа', data.message || "Неверный email или пароль", false);
        }
    } catch (error) {
        console.error(error);
        showModal('Ошибка сети', 'Сервер библиотеки не отвечает. Проверьте, запущен ли dotnet run.', false);
    } finally {
        btn.innerText = originalText;
        btn.disabled = false;
    }
});

function showModal(title, message, isSuccess) {
    const modal = document.getElementById('customModal');
    
    // Текст и иконки
    document.getElementById('modalTitle').innerText = title;
    document.getElementById('modalMessage').innerText = message;
    document.getElementById('modalIcon').innerText = isSuccess ? '✨' : '🚫';
    
    // Показываем модальное окно
    modal.style.display = 'flex';
    modal.classList.add('active');
}

function closeModal() {
    const modal = document.getElementById('customModal');
    
    modal.classList.remove('active');
    setTimeout(() => {
        modal.style.display = 'none';
    }, 300);
}

// Закрытие по клику вне окна
window.addEventListener('click', function(event) {
    const modal = document.getElementById('customModal');
    if (event.target === modal) {
        closeModal();
    }
});