document.addEventListener('DOMContentLoaded', () => {
    
    // ==========================================
    // 0. КОНФИГУРАЦИЯ
    // ==========================================
    const API_URL = "http://localhost:5283/api"; 

    // Утилиты для localStorage
    const getUser = () => JSON.parse(localStorage.getItem('user'));
    const getToken = () => localStorage.getItem('token');
    const updateUser = (newData) => localStorage.setItem('user', JSON.stringify(newData));

    // ==========================================
    // 1. ИНТЕРФЕЙС (Header)
    // ==========================================
    const header = document.querySelector('.header');
    if (header) {
        window.addEventListener('scroll', () => {
            header.classList.toggle('scrolled', window.scrollY > 50);
        });
    }

    // ==========================================
    // 2. АВТОРИЗАЦИЯ
    // ==========================================
    
    // --- РЕГИСТРАЦИЯ ---
    const registerForm = document.getElementById('registerForm');
    if (registerForm) {
        registerForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const username = document.getElementById('username').value;
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            const confirm = document.getElementById('confirm').value;

            if (password !== confirm) {
                showModal({ title: "Ошибка", text: "Пароли не совпадают!", showCancel: false });
                return;
            }

            try {
                const response = await fetch(`${API_URL}/auth/register`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ username, email, password })
                });

                if (response.ok) {
                    showModal({ 
                        title: "Успех!", 
                        text: "Регистрация успешна. Войдите в аккаунт.", 
                        showCancel: false, 
                        onConfirm: () => window.location.href = "login.html" 
                    });
                } else {
                    const data = await response.json();
                    showModal({ title: "Ошибка", text: data.message || "Сбой регистрации", showCancel: false });
                }
            } catch (error) {
                showModal({ title: "Ошибка", text: "Сервер недоступен", showCancel: false });
            }
        });
    }

    // --- ВХОД ---
    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', async (e) => {
            e.preventDefault();
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
                    localStorage.setItem('token', data.token); 
                    localStorage.setItem('user', JSON.stringify(data.user));
                    window.location.href = "profile.html";
                } else {
                    showModal({ title: "Ошибка", text: data.message || "Неверные данные", showCancel: false });
                }
            } catch (error) {
                showModal({ title: "Ошибка", text: "Сервер недоступен", showCancel: false });
            }
        });
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
    // 3. ЛОГИКА ПРОФИЛЯ
    // ==========================================
    if (document.getElementById('profile-username')) {
        const user = getUser();
        const token = getToken();
        
        if (!user || !token) {
            window.location.href = "login.html";
        } else {
            // Заполняем данные
            document.getElementById('profile-username').innerText = user.username;
            document.getElementById('profile-email').innerText = user.email;
            document.getElementById('profile-emeralds').innerText = user.emeralds || 0;

            // Запускаем функции
            renderStreak(user);        
            loadUserCollection(token); 
            renderBorrowedBooks(token);
            renderCalendar(); 
        }
    }

    // ==========================================
    // 4. КАТАЛОГ КНИГ
    // ==========================================
    if (document.getElementById('booksGrid')) {
        loadBooks();
    }

    async function loadBooks() {
        const container = document.getElementById('booksGrid');
        container.innerHTML = '<p style="text-align:center; width:100%;">Загрузка библиотеки...</p>';

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
            container.innerHTML = '<p style="text-align:center; color:red;">Не удалось загрузить книги.</p>';
        }
    }

    // ==========================================
    // 4.1. ДЕТАЛИ КНИГИ И КОММЕНТАРИИ
    // ==========================================
    if (document.getElementById('commentsList')) {
        const urlParams = new URLSearchParams(window.location.search);
        const bookId = urlParams.get('id');
        if (bookId) {
            loadComments(bookId);
        }
    }

    // ==========================================
    // 5. ЛОГИКА СТРИКОВ (ОГОНЬ)
    // ==========================================
    function renderStreak(user) {
        const badge = document.getElementById('streak-badge');
        const countEl = document.getElementById('streak-count');
        
        if (!badge || !countEl) return;

        badge.style.display = 'flex';

        if (user.streakLost) {
            badge.className = 'streak-badge fire-lost';
            countEl.innerText = user.savedStreak || 0;
            badge.onclick = () => showRestoreModal(user);
            return;
        }

        const streak = user.streak || 1;
        countEl.innerText = streak;
        
        badge.className = 'streak-badge'; 
        if (streak >= 100) badge.classList.add('fire-lvl-5');
        else if (streak >= 60) badge.classList.add('fire-lvl-5');
        else if (streak >= 30) badge.classList.add('fire-lvl-4');
        else if (streak >= 15) badge.classList.add('fire-lvl-3');
        else if (streak >= 7)  badge.classList.add('fire-lvl-2');
        else                   badge.classList.add('fire-lvl-1');

        badge.onclick = () => {
            const nextTarget = getNextLevelTarget(streak);
            const daysLeft = nextTarget - streak;
            let text = `Ты заходишь в библиотеку ${streak} дн. подряд!`;
            if (daysLeft > 0) text += `\nДо следующего уровня огня: ${daysLeft} дн.`;
            else text += `\nТы легенда огня! 🔥`;

            showModal({ title: "Ударный режим 🔥", text: text, showCancel: false });
        };
    }

    function getNextLevelTarget(current) {
        if (current < 7) return 7;
        if (current < 15) return 15;
        if (current < 30) return 30;
        if (current < 60) return 60;
        return 100;
    }

    function showRestoreModal(user) {
        const COST = 50;
        showModal({
            title: "Огонь погас! ❄️",
            text: `Ты пропустил день. Твой стрик (${user.savedStreak} дн.) сгорел. Восстановить за ${COST} 💎?`,
            icon: "😱",
            onConfirm: () => restoreStreakAction(COST)
        });
    }

    async function restoreStreakAction(cost) {
        const token = getToken();
        let user = getUser();

        if ((user.emeralds || 0) < cost) {
            showModal({ title: "Ошибка", text: "Недостаточно изумрудов!", showCancel: false });
            return;
        }

        try {
            const response = await fetch(`${API_URL}/users/restore-streak`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.ok) {
                const data = await response.json();
                user.emeralds = data.newEmeralds;
                user.streak = data.restoredStreak;
                user.streakLost = false;
                user.savedStreak = 0;
                updateUser(user);

                document.getElementById('profile-emeralds').innerText = user.emeralds;
                renderStreak(user);

                showModal({ title: "Успех!", text: "Огонь снова горит! 🔥", showCancel: false, icon: "✨" });
            } else {
                showModal({ title: "Ошибка", text: "Не удалось восстановить.", showCancel: false });
            }
        } catch (e) {
            console.error(e);
        }
    }

    // ==========================================
    // 6. ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ
    // ==========================================
    
    async function loadUserCollection(token) {
        const container = document.getElementById('collectionContainer');
        if(!container) return;

        try {
            const response = await fetch(`${API_URL}/shop/my-cards`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            const cards = await response.json();
            if (cards.length === 0) {
                container.innerHTML = '<p style="color: var(--text-muted);">Коллекция пуста.</p>';
                return;
            }
            const getRankColor = (r) => ({'S':'#ffd700','A':'#9b5de5','B':'#00bbf9','C':'#00f5d4','D':'#a8a8a8','E':'#cd7f32'}[r.toUpperCase()] || 'transparent');
            
            container.innerHTML = `<div class="collection-grid">${cards.map(card => `
                <div class="collection-item scroll-reveal" style="border-color: ${getRankColor(card.rank)}">
                    <img src="${card.image || 'assets/images/card/common/D.jpg'}" alt="${card.name}">
                    <div class="rank-tag">${card.rank}</div>
                </div>`).join('')}</div>`;
        } catch (err) {
            container.innerHTML = '<p style="color: var(--status-unavailable);">Ошибка загрузки карт.</p>';
        }
    }

    async function renderBorrowedBooks(token) {
        const list = document.getElementById('borrowedBooksList');
        const empty = document.getElementById('noBooksMessage');
        if(!list) return;

        try {
            const response = await fetch(`${API_URL}/library/my-books`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            const books = await response.json();

            if (books.length === 0) {
                list.innerHTML = '';
                if(empty) empty.style.display = 'block';
                return;
            }

            if(empty) empty.style.display = 'none';
            list.innerHTML = books.map(b => `
                <div style="background: rgba(255,255,255,0.6); padding:1.5rem; margin-bottom:1rem; border-radius:14px; display:flex; justify-content:space-between; align-items:center; border: 1px solid var(--border); box-shadow: 0 2px 10px rgba(0,0,0,0.05);">
                    <div style="display: flex; align-items: center; gap: 1rem;">
                        <div style="font-size: 2rem;">📖</div>
                        <div>
                            <strong style="font-size:1.2rem; color:var(--text); font-family: var(--font-heading);">${b.title}</strong><br>
                            <span style="color:var(--text-muted);">${b.author}</span>
                        </div>
                    </div>
                    <div style="display:flex; gap:10px;">
                        ${b.fileUrl ? `<a href="${b.fileUrl}" target="_blank" class="btn btn-primary" style="padding: 0.5rem 1rem; font-size: 0.9rem;">Читать</a>` : ''}
                        <button onclick="window.handleReturnBook(${b.id})" class="btn btn-secondary" style="padding: 0.5rem 1rem; font-size: 0.9rem;">Вернуть</button>
                    </div>
                </div>`).join('');
        } catch (err) {
            list.innerHTML = '<p style="color: var(--status-unavailable);">Не удалось загрузить полку.</p>';
        }
    }

    async function renderCalendar() {
        const calendar = document.getElementById('readingCalendar');
        if (!calendar) return;

        const token = getToken();
        let activeDates = [];

        try {
            const response = await fetch(`${API_URL}/progress/activity`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (response.ok) {
                activeDates = await response.json(); 
            }
        } catch (e) {
            console.error("Ошибка загрузки календаря:", e);
        }

        const today = new Date();
        let html = '';

        for (let i = 19; i >= 0; i--) {
            const d = new Date();
            d.setDate(today.getDate() - i);
            
            const year = d.getFullYear();
            const month = String(d.getMonth() + 1).padStart(2, '0');
            const day = String(d.getDate()).padStart(2, '0');
            const dateString = `${year}-${month}-${day}`;

            const isToday = (i === 0);
            const isActive = activeDates.includes(dateString) || isToday; 

            let bg = 'rgba(139, 69, 19, 0.1)';
            let color = 'var(--text-muted)';
            let border = '1px solid transparent';
            
            if (isToday) {
                bg = '#8B4513';
                color = '#fff';
            } else if (isActive) {
                bg = '#E6DfbF';
                color = '#5a3a22';
            }

            html += `
                <div class="calendar-day" 
                     style="background:${bg}; color:${color}; border:${border}" 
                     title="${dateString}">
                     ${d.getDate()}
                </div>`;
        }
        calendar.innerHTML = html;
    }

    // ==========================================
    // 7. УНИВЕРСАЛЬНОЕ МОДАЛЬНОЕ ОКНО
    // ==========================================
    window.showModal = function({ title, text, icon, onConfirm, showCancel = true }) {
        let modal = document.getElementById('appModal');
        if (!modal) {
            createModalMarkup();
            modal = document.getElementById('appModal');
        }

        document.getElementById('modalTitle').innerText = title;
        document.getElementById('modalText').innerText = text;
        document.getElementById('modalIcon').innerText = icon || '🔔';
        
        const actionsEl = document.getElementById('modalActions');
        actionsEl.innerHTML = ''; 

        if (showCancel) {
            const cancelBtn = document.createElement('button');
            cancelBtn.className = 'btn-modal btn-cancel';
            cancelBtn.innerText = 'Отмена';
            cancelBtn.onclick = closeModal;
            actionsEl.appendChild(cancelBtn);
        }

        const confirmBtn = document.createElement('button');
        confirmBtn.className = 'btn-modal btn-confirm';
        confirmBtn.innerText = showCancel ? 'Подтвердить' : 'ОК';
        confirmBtn.onclick = () => {
            if (onConfirm) onConfirm();
            closeModal();
        };
        actionsEl.appendChild(confirmBtn);

        modal.classList.add('active');
    };

    window.closeModal = function() {
        const modal = document.getElementById('appModal');
        if(modal) modal.classList.remove('active');
    };

    function createModalMarkup() {
        const div = document.createElement('div');
        div.id = 'appModal';
        div.className = 'modal-overlay';
        div.innerHTML = `
            <div class="modal-window">
                <span id="modalIcon" class="modal-icon">🔔</span>
                <h3 id="modalTitle" class="modal-title"></h3>
                <p id="modalText" class="modal-text"></p>
                <div id="modalActions" class="modal-actions"></div>
            </div>
        `;
        document.body.appendChild(div);
    }
});

// ==========================================
// 8. ГЛОБАЛЬНЫЕ ФУНКЦИИ (ДЛЯ ONCLICK)
// ==========================================

window.takeBook = async function(bookId, title) {
    const token = localStorage.getItem('token');
    if (!token) {
        window.location.href = "login.html";
        return;
    }
    const API_URL = "http://localhost:5283/api";
    
    try {
        const response = await fetch(`${API_URL}/library/borrow/${bookId}`, {
            method: 'POST',
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            showModal({ title: "Успех", text: `Книга "${title}" добавлена на полку!`, showCancel: false, icon: "📖" });
        } else {
            const data = await response.json();
            showModal({ title: "Ошибка", text: data.message || "Ошибка", showCancel: false });
        }
    } catch (error) {
        showModal({ title: "Ошибка", text: "Ошибка связи с сервером", showCancel: false });
    }
};

window.handleReturnBook = function(bookId) {
    const API_URL = "http://localhost:5283/api";
    
    showModal({
        title: "Вернуть книгу?",
        text: "Вы уверены, что хотите вернуть книгу?",
        icon: "👋",
        onConfirm: async () => {
            const token = localStorage.getItem('token');
            try {
                const response = await fetch(`${API_URL}/library/return/${bookId}`, {
                    method: 'DELETE',
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                if (response.ok) {
                    showModal({ 
                        title: "Возвращено", 
                        text: "Книга возвращена в библиотеку.", 
                        showCancel: false,
                        onConfirm: () => window.location.reload() 
                    });
                }
            } catch (err) {
                console.error(err);
            }
        }
    });
};

// ==========================================
// 9. КОММЕНТАРИИ (НОВОЕ)
// ==========================================

async function loadComments(bookId) {
    const API_URL = "http://localhost:5283/api";
    const list = document.getElementById('commentsList');
    const token = localStorage.getItem('token');
    
    const headers = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;

    try {
        const response = await fetch(`${API_URL}/comments/${bookId}`, { headers });
        const comments = await response.json();

        if (comments.length === 0) {
            list.innerHTML = '<p style="color: var(--text-muted);">Пока нет комментариев. Будьте первым!</p>';
            return;
        }

        list.innerHTML = comments.map(c => `
            <div class="comment-card ${c.isMyComment ? 'my-comment' : ''}">
                <div class="comment-header">
                    <span class="comment-author">${c.username}</span>
                    <span class="comment-date">${new Date(c.createdAt).toLocaleDateString()}</span>
                </div>
                <div class="comment-text">${c.text}</div>
            </div>
        `).join('');

    } catch (error) {
        console.error(error);
        list.innerHTML = '<p style="color: red;">Не удалось загрузить комментарии.</p>';
    }
}

window.postComment = async function() {
    const API_URL = "http://localhost:5283/api";
    const urlParams = new URLSearchParams(window.location.search);
    const bookId = urlParams.get('id');

    if (!bookId) return;

    const input = document.getElementById('commentInput');
    const text = input.value.trim();
    const token = localStorage.getItem('token');

    if (!token) {
        showModal({ title: "Внимание", text: "Войдите, чтобы оставлять комментарии.", showCancel: false });
        return;
    }

    if (!text) return;

    try {
        const response = await fetch(`${API_URL}/comments`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({ bookId: bookId, text: text })
        });

        if (response.ok) {
            input.value = ''; 
            loadComments(bookId); 
        } else {
            showModal({ title: "Ошибка", text: "Не удалось отправить комментарий.", showCancel: false });
        }
    } catch (e) {
        console.error(e);
    }
};

// ==========================================
// 10. МАГАЗИН (ПОКУПКА КАРТ)
// ==========================================
window.buyCard = async function(cardId, price, cardName) {
    const user = JSON.parse(localStorage.getItem('user'));
    
    // 1. Проверка авторизации
    if (!user) {
        showModal({ title: "Ошибка", text: "Войдите, чтобы покупать карты!", showCancel: false });
        return;
    }

    // 2. Проверка баланса (визуальная)
    if ((user.emeralds || 0) < price) {
        showModal({ 
            title: "Не хватает средств", 
            text: `У вас ${user.emeralds} 💎, а нужно ${price}.`, 
            icon: "💎", 
            showCancel: false 
        });
        return;
    }

    // 3. Окно подтверждения
    showModal({
        title: "Покупка карты",
        text: `Купить "${cardName}" за ${price} 💎?`,
        icon: "🛒",
        onConfirm: async () => {
            const token = localStorage.getItem('token');
            const API_URL = "http://localhost:5283/api"; 

            try {
                // Отправляем запрос на покупку
                const response = await fetch(`${API_URL}/shop/buy/${cardId}`, {
                    method: 'POST',
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                const data = await response.json();

                if (response.ok) {
                    // Обновляем баланс
                    user.emeralds = data.newBalance; 
                    localStorage.setItem('user', JSON.stringify(user));
                    
                    // Обновляем UI, если есть счетчик
                    const gemEl = document.getElementById('profile-emeralds');
                    if(gemEl) gemEl.innerText = user.emeralds;

                    showModal({ 
                        title: "Успешно!", 
                        text: `Вы получили карту: ${cardName}!`, 
                        icon: "✨", 
                        showCancel: false 
                    });
                } else {
                    showModal({ title: "Ошибка", text: data.message || "Не удалось купить.", showCancel: false });
                }
            } catch (e) {
                console.error(e);
                showModal({ title: "Ошибка", text: "Сервер недоступен.", showCancel: false });
            }
        }
    });
};