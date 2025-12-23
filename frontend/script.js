document.addEventListener('DOMContentLoaded', function() {
    
    // ==========================================
    // 0. НАСТРОЙКИ (Конфигурация API)
    // ==========================================
    const HOST = "localhost:5283";
    const API_URL = `http://${HOST}/api`; 

    // Вспомогательная функция для получения токена
    const getAuthHeader = () => {
        const token = localStorage.getItem('token');
        return token ? { 'Authorization': `Bearer ${token}` } : {};
    };

    // ==========================================
    // 1. АНИМАЦИИ И ЖИВОЙ ИНТЕРФЕЙС
    // ==========================================
    const header = document.querySelector('.header');
    if (header) {
        window.addEventListener('scroll', () => {
            header.classList.toggle('scrolled', window.scrollY > 50);
        });
    }
    
    // Плавные переходы для всех кнопок
    document.querySelectorAll('.btn, .nav-link').forEach(el => {
        el.style.transition = 'all 0.3s cubic-bezier(0.25, 0.46, 0.45, 0.94)';
    });

    // ==========================================
    // 2. АВТОРИЗАЦИЯ (Вход и Регистрация)
    // ==========================================

    // --- РЕГИСТРАЦИЯ ---
    const registerForm = document.getElementById('registerForm');
    if (registerForm) {
        registerForm.addEventListener('submit', async (e) => {
            e.preventDefault(); // Стоп перезагрузка

            const username = document.getElementById('username').value;
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            const confirm = document.getElementById('confirm')?.value;

            if (confirm && password !== confirm) {
                alert("Пароли не совпадают!");
                return;
            }

            try {
                const response = await fetch(`${API_URL}/auth/register`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ username, email, password })
                });

                const data = await response.json();

                if (response.ok) {
                    alert("Регистрация успешна! Теперь войдите.");
                    window.location.href = "login.html";
                } else {
                    alert("Ошибка: " + (data.message || "Сбой регистрации"));
                }
            } catch (error) {
                console.error("Ошибка сети:", error);
                alert("Нет связи с сервером!");
            }
        });
    }

    // --- ВХОД (LOGIN) ---
    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', async (e) => {
            e.preventDefault(); // Стоп перезагрузка
            
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            
            try {
                const response = await fetch(`${API_URL}/auth/login`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ email, password })
                });

                const data = await response.json();

                if (response.ok) {
                    // СОХРАНЯЕМ ВСЁ ПРАВИЛЬНО
                    localStorage.setItem('token', data.token); 
                    localStorage.setItem('user', JSON.stringify(data.user));
                    
                    window.location.href = "profile.html";
                } else {
                    alert("Ошибка: " + (data.message || "Неверный логин или пароль"));
                }
            } catch (error) {
                alert("Ошибка подключения! Проверьте бэкенд.");
            }
        });
    }

    // ==========================================
    // 3. ПРОФИЛЬ (Отображение данных)
    // ==========================================
    if (window.location.pathname.includes('profile.html')) {
        const userJson = localStorage.getItem('user');
        const token = localStorage.getItem('token');
        
        if (!userJson || !token) {
            window.location.href = "login.html";
        } else {
            const user = JSON.parse(userJson);
            // Заполняем поля, если они есть на странице
            const fields = {
                'profile-username': user.username,
                'profile-email': user.email,
                'profile-emeralds': user.emeralds
            };

            for (let id in fields) {
                const el = document.getElementById(id);
                if (el) el.innerText = fields[id] || "0";
            }
        }
    }

    // --- ВЫХОД ---
    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', () => {
            localStorage.clear();
            window.location.href = "index.html";
        });
    }

    // ==========================================
    // 4. КАТАЛОГ (Компактные карточки)
    // ==========================================
    if (window.location.pathname.includes('catalog.html')) {
        loadBooks();
    }

    async function loadBooks() {
        const container = document.getElementById('booksGrid');
        if (!container) return;

        container.innerHTML = '<p style="text-align:center; width:100%; color: var(--text-muted);">Открываем архивы...</p>';

        try {
            const response = await fetch(`${API_URL}/books`);
            const books = await response.json();
            
            if (books.length === 0) {
                container.innerHTML = '<p style="text-align:center; width:100%;">Библиотека пуста.</p>';
                return;
            }

            container.innerHTML = ''; 

            books.forEach((book, index) => {
                const card = document.createElement('div');
                card.className = 'book-card fade-in';
                card.style.animationDelay = `${index * 0.05}s`;
                
                // Красивая компактная карточка (только фото и название под ним)
                card.innerHTML = `
                    <div class="book-cover-wrapper" onclick="window.location.href='book-details.html?id=${book.id}'">
                        <img src="${book.coverImage || 'assets/images/placeholder-book.jpg'}" 
                             class="book-cover" 
                             onerror="this.src='https://via.placeholder.com/200x300?text=No+Cover'">
                    </div>
                    <div class="book-title" onclick="window.location.href='book-details.html?id=${book.id}'">
                        ${book.title}
                    </div>
                `;
                container.appendChild(card);
            });

        } catch (error) {
            container.innerHTML = '<p style="text-align:center; color:red;">Ошибка загрузки каталога.</p>';
        }
    }
});

// ==========================================
// 5. ГЛОБАЛЬНЫЕ ФУНКЦИИ (Для работы с БД)
// ==========================================

// Взять книгу на полку
window.takeBook = async function(bookId, title) {
    const token = localStorage.getItem('token');
    if (!token) {
        alert("Пожалуйста, войдите в аккаунт!");
        window.location.href = "login.html";
        return;
    }
    
    try {
        const response = await fetch(`http://localhost:5283/api/library/borrow/${bookId}`, {
            method: 'POST',
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        const data = await response.json();

        if (response.ok) {
            alert(`Книга "${title}" добавлена на вашу полку!`);
            if (typeof renderBorrowedBooks === 'function') renderBorrowedBooks(); // Обновить если мы в профиле
        } else {
            alert(data.message || "Ошибка");
        }

    } catch (error) {
        alert("Ошибка сервера!");
    }
};