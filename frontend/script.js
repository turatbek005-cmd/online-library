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
    // 2. ЛОГИКА БЭКЕНДА (АВТОРИЗАЦИЯ)
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
                console.log("Ответ сервера:", data); 

                if (response.ok) {
                    if (!data.user) {
                        alert("Ошибка: Сервер не вернул данные пользователя!");
                        return;
                    }
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
        
        if (!userJson || userJson === "undefined" || userJson === "null") {
            window.location.href = "login.html";
            return;
        }

        try {
            const user = JSON.parse(userJson);
            const usernameEl = document.getElementById('profile-username');
            const emailEl = document.getElementById('profile-email');
            const emeraldsEl = document.getElementById('profile-emeralds');

            if (usernameEl) usernameEl.innerText = user.username || "Неизвестный";
            if (emailEl) emailEl.innerText = user.email || "Нет email";
            if (emeraldsEl) emeraldsEl.innerText = user.emeralds || 0;

        } catch (e) {
            localStorage.removeItem('user');
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

    // ==========================================
    // 3. ЛОГИКА КАТАЛОГА (Загрузка книг из БД)
    // ==========================================
    if (window.location.pathname.includes('catalog.html')) {
        loadBooks();
    }

    async function loadBooks() {
        const container = document.getElementById('booksGrid'); // Ищем сетку книг
        if (!container) return;

        container.innerHTML = '<p style="text-align:center; width:100%;">Загрузка книг...</p>';

        try {
            // Запрос к нашему BooksController
            const response = await fetch(`${API_URL}/books`);
            if (!response.ok) throw new Error("Ошибка загрузки");
            
            const books = await response.json();
            
            if (books.length === 0) {
                container.innerHTML = '<p style="text-align:center; width:100%;">Библиотека пока пуста.</p>';
                return;
            }

            container.innerHTML = ''; // Очищаем "Загрузку..."

            // Рисуем книги
            books.forEach(book => {
                const image = (book.coverImage && book.coverImage.length > 5) 
                    ? `<img src="${book.coverImage}" class="book-cover">` 
                    : `<div class="book-cover">📖</div>`;

                const card = document.createElement('div');
                card.className = 'book-card fade-in';
                
                // Вот здесь мы добавляем кнопку ЧИТАТЬ
                card.innerHTML = `
                    ${image}
                    <div class="book-info">
                        <h3 class="book-title">${book.title}</h3>
                        <p class="book-author">${book.author}</p>
                        
                        <div class="book-footer" style="display: flex; gap: 10px; margin-top: auto;">
                            <!-- Кнопка ВЗЯТЬ -->
                            <button class="btn btn-secondary btn-small" style="flex: 1;" 
                                onclick="takeBook(${book.id}, '${book.title.replace(/'/g, "\\'")}')">
                                Взять
                            </button>

                            <!-- Кнопка ЧИТАТЬ (Новая) -->
                            <a href="${book.fileUrl}" target="_blank" rel="noreferrer" 
                               class="btn btn-primary btn-small" 
                               style="flex: 1; text-align: center; text-decoration: none; display: flex; align-items: center; justify-content: center;">
                                Читать
                            </a>
                        </div>
                    </div>
                `;
                
                container.appendChild(card);
            });

        } catch (error) {
            console.error(error);
            container.innerHTML = '<p style="text-align:center; color:red;">Не удалось загрузить книги.</p>';
        }
    }
});

// ==========================================
// 4. ГЛОБАЛЬНАЯ ФУНКЦИЯ: ВЗЯТЬ КНИГУ (ЧЕРЕЗ БД)
// ==========================================
window.takeBook = async function(bookId, title) {
    // 1. Проверяем вход
    const userJson = localStorage.getItem('user');
    if (!userJson) {
        alert("Сначала войдите в аккаунт!");
        window.location.href = "login.html";
        return;
    }
    
    const user = JSON.parse(userJson);
    const API_URL = "http://localhost:5283/api"; // <-- ПОРТ

    // 2. Отправляем запрос на сервер (в LibraryController)
    try {
        const response = await fetch(`${API_URL}/library/borrow`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId: user.id, bookId: bookId })
        });

        const data = await response.json();

        if (response.ok) {
            alert(`Книга "${title}" добавлена на полку!`);
        } else {
            alert("Ошибка: " + (data.message || "Не удалось взять книгу"));
        }

    } catch (error) {
        console.error(error);
        alert("Ошибка сервера!");
    }
};