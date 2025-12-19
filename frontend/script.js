document.addEventListener('DOMContentLoaded', function() {
    
    // ==========================================
    // 0. НАСТРОЙКИ (ПОРТ БЭКЕНДА)
    // ==========================================
    // ❗ ПРОВЕРЬ ПОРТ! (Посмотри в терминале dotnet run)
    const API_URL = "http://localhost:5283/api"; 

    // ==========================================
    // 1. АНИМАЦИИ
    // ==========================================
    const fadeElements = document.querySelectorAll('.fade-in');
    const fadeInOnScroll = () => {
        fadeElements.forEach(element => {
            const elementTop = element.getBoundingClientRect().top;
            if (elementTop < window.innerHeight - 150) {
                element.classList.add('visible');
            }
        });
    };
    fadeInOnScroll();
    window.addEventListener('scroll', fadeInOnScroll);
    
    // Хедер и переходы
    const header = document.querySelector('.header');
    if (header) {
        window.addEventListener('scroll', () => {
            if (window.scrollY > 50) header.classList.add('scrolled');
            else header.classList.remove('scrolled');
        });
    }
    
    document.querySelectorAll('.btn, .nav-link, .book-card')
        .forEach(el => el.style.transition = 'all 0.4s cubic-bezier(0.25, 0.46, 0.45, 0.94)');

    // ==========================================
    // 2. ЛОГИКА БЭКЕНДА
    // ==========================================

    // --- РЕГИСТРАЦИЯ ---
    const registerForm = document.getElementById('registerForm');
    if (registerForm) {
        registerForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const username = document.getElementById('username').value;
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;

            try {
                const response = await fetch(`${API_URL}/auth/register`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ username, email, password })
                });

                const data = await response.json();

                if (response.ok) {
                    alert("Успешно! Теперь войдите.");
                    window.location.href = "login.html";
                } else {
                    alert("Ошибка: " + (data.message || "Сбой регистрации"));
                }
            } catch (error) {
                console.error(error);
                alert("Нет связи с сервером!");
            }
        });
    }

    // --- ВХОД (LOGIN) ---
    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            
            console.log("Отправляем запрос на вход:", email);

            try {
                const response = await fetch(`${API_URL}/auth/login`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ email, password })
                });

                const data = await response.json();
                console.log("Ответ сервера:", data); // <-- СМОТРИМ, ЧТО ПРИШЛО

                if (response.ok) {
                    // ПРОВЕРКА: Есть ли внутри user?
                    if (!data.user) {
                        alert("Ошибка: Сервер не вернул данные пользователя!");
                        console.error("BAD RESPONSE:", data);
                        return;
                    }

                    // Сохраняем ТОЛЬКО если данные валидны
                    localStorage.setItem('user', JSON.stringify(data.user));
                    console.log("Данные сохранены, переходим в профиль...");
                    window.location.href = "profile.html";
                } else {
                    alert("Ошибка: " + (data.message || "Неверные данные"));
                }
            } catch (error) {
                console.error("Ошибка сети:", error);
                alert("Ошибка подключения! Бэкенд запущен?");
            }
        });
    }

    // --- ПРОФИЛЬ (Загрузка данных) ---
    if (window.location.pathname.includes('profile.html')) {
        const userJson = localStorage.getItem('user');
        
        // 1. Проверяем, есть ли данные вообще
        if (!userJson || userJson === "undefined" || userJson === "null") {
            console.warn("Нет данных о пользователе, редирект на логин.");
            window.location.href = "login.html";
            return;
        }

        try {
            const user = JSON.parse(userJson);
            console.log("Загружен профиль для:", user);

            // 2. Ищем элементы
            const usernameEl = document.getElementById('profile-username');
            const emailEl = document.getElementById('profile-email');
            const emeraldsEl = document.getElementById('profile-emeralds');

            // 3. Вставляем данные (с проверкой, что элементы найдены)
            if (usernameEl) usernameEl.innerText = user.username || "Неизвестный";
            if (emailEl) emailEl.innerText = user.email || "Нет email";
            if (emeraldsEl) emeraldsEl.innerText = user.emeralds || 0;

        } catch (e) {
            console.error("КРИТИЧЕСКАЯ ОШИБКА ДАННЫХ:", e);
            // Если данные битые — чистим их, чтобы не было вечного цикла
            localStorage.removeItem('user');
            alert("Ошибка данных профиля. Пожалуйста, войдите снова.");
            window.location.href = "login.html";
        }
    }

    // --- ВЫХОД ---
    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', () => {
            localStorage.removeItem('user');
            window.location.href = "index.html";
        });
    }
});