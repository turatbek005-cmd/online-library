document.addEventListener('DOMContentLoaded', function() {
    
    // ==========================================
    // 0. НАСТРОЙКИ (Конфигурация)
    // ==========================================
    const API_URL = "http://localhost:5283/api"; 

    // ==========================================
    // 1. ЖИВОЙ ИНТЕРФЕЙС И АНИМАЦИИ
    // ==========================================
    const header = document.querySelector('.header');
    if (header) {
        window.addEventListener('scroll', () => {
            header.classList.toggle('scrolled', window.scrollY > 50);
        });
    }

    // Плавные переходы для кнопок
    document.querySelectorAll('.btn, .nav-link').forEach(el => {
        el.style.transition = 'all 0.3s cubic-bezier(0.25, 0.46, 0.45, 0.94)';
    });

    // ==========================================
    // 2. АВТОРИЗАЦИЯ (РЕГИСТРАЦИЯ)
    // ==========================================
    const registerForm = document.getElementById('registerForm');
    if (registerForm) {
        registerForm.addEventListener('submit', async (e) => {
            e.preventDefault(); // КРИТИЧНО: отменяет обновление страницы
            
            const username = document.getElementById('username').value;
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            const confirm = document.getElementById('confirm').value;

            if (password !== confirm) {
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
                    alert("Успешно! Теперь войдите в аккаунт.");
                    window.location.href = "login.html";
                } else {
                    alert("Ошибка: " + (data.message || "Сбой регистрации"));
                }
            } catch (error) {
                console.error("Ошибка сети:", error);
                alert("Сервер не отвечает!");
            }
        });
    }

    // ==========================================
    // 3. АВТОРИЗАЦИЯ (ВХОД)
    // ==========================================
    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', async (e) => {
            e.preventDefault(); // КРИТИЧНО: отменяет обновление страницы
            
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
                    // Сохраняем данные для всех страниц
                    localStorage.setItem('token', data.token); 
                    localStorage.setItem('user', JSON.stringify(data.user));
                    
                    window.location.href = "profile.html";
                } else {
                    alert("Ошибка: " + (data.message || "Неверный логин или пароль"));
                }
            } catch (error) {
                console.error(error);
                alert("Ошибка связи с сервером");
            }
        });
    }

    // ==========================================
    // 4. ПРОФИЛЬ (ОТОБРАЖЕНИЕ)
    // ==========================================
    if (window.location.pathname.includes('profile.html')) {
        const userJson = localStorage.getItem('user');
        const token = localStorage.getItem('token');
        
        if (!userJson || !token) {
            window.location.href = "login.html";
        } else {
            const user = JSON.parse(userJson);
            // Заполняем элементы если они есть
            if (document.getElementById('profile-username')) 
                document.getElementById('profile-username').innerText = user.username;
            if (document.getElementById('profile-email')) 
                document.getElementById('profile-email').innerText = user.email;
            if (document.getElementById('profile-emeralds')) 
                document.getElementById('profile-emeralds').innerText = user.emeralds || 0;
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
    // 5. КАТАЛОГ (ЗАГРУЗКА КНИГ)
    // ==========================================
    if (window.location.pathname.includes('catalog.html')) {
        loadBooks();
    }

    async function loadBooks() {
        const container = document.getElementById('booksGrid');
        if (!container) return;

        container.innerHTML = '<p style="text-align:center; width:100%;">Загрузка знаний...</p>';

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
                
                // Используем новый компактный стиль (только фото и название)
                card.innerHTML = `
                    <div class="book-cover-wrapper" onclick="window.location.href='book-details.html?id=${book.id}'">
                        <img src="${book.coverImage || 'assets/images/placeholder.jpg'}" 
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
            console.error(error);
            container.innerHTML = '<p style="text-align:center; color:red;">Ошибка сервера.</p>';
        }
    }
});

// ==========================================
// 6. ГЛОБАЛЬНАЯ ФУНКЦИЯ: ВЗЯТЬ КНИГУ (ДЛЯ КНОПОК)
// ==========================================
window.takeBook = async function(bookId, title) {
    const token = localStorage.getItem('token');
    if (!token) {
        alert("Сначала войдите в аккаунт!");
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
        } else {
            alert(data.message || "Ошибка");
        }
    } catch (error) {
        alert("Ошибка связи с сервером!");
    }
};