const API_URL = "http://localhost:5283/api";

document.addEventListener('DOMContentLoaded', () => {
    loadUserProfile();
    loadMyBooks();
});

// 1. ЗАГРУЗКА ДАННЫХ ПОЛЬЗОВАТЕЛЯ
async function loadUserProfile() {
    const token = localStorage.getItem('token');
    if (!token) { window.location.href = 'login.html'; return; }

    try {
        const userStr = localStorage.getItem('user');
        if (userStr) {
            const user = JSON.parse(userStr);
            
            const nameEl = document.getElementById('profile-username');
            const emailEl = document.getElementById('profile-email');
            const gemsEl = document.getElementById('profile-emeralds');
            
            if(nameEl) nameEl.innerText = user.username;
            if(emailEl) emailEl.innerText = user.email;
            if(gemsEl) gemsEl.innerText = user.emeralds;
            
            if(user.streak > 0) {
                const badge = document.getElementById('streak-badge');
                if(badge) {
                    badge.style.display = 'flex';
                    document.getElementById('streak-count').innerText = user.streak;
                }
            }
        }
    } catch (e) {
        console.error("Ошибка загрузки профиля", e);
    }

    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', () => {
            localStorage.removeItem('token');
            localStorage.removeItem('user');
            window.location.href = 'login.html';
        });
    }
}

// 2. ЗАГРУЗКА КНИГ (БЕЗ КАРТИНКИ, СТАРЫЙ СТИЛЬ)
async function loadMyBooks() {
    const token = localStorage.getItem('token');
    const list = document.getElementById('borrowedBooksList');
    const noBooks = document.getElementById('noBooksMessage');

    if (!list) return;

    try {
        const res = await fetch(`${API_URL}/library/my-books`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        
        if (!res.ok) throw new Error("Ошибка сервера");
        
        const books = await res.json();

        if (books.length === 0) {
            list.style.display = 'none';
            if(noBooks) noBooks.style.display = 'block';
        } else {
            if(noBooks) noBooks.style.display = 'none';
            list.style.display = 'grid';
            list.style.gridTemplateColumns = 'repeat(auto-fill, minmax(200px, 1fr))';
            list.style.gap = '20px';

            list.innerHTML = books.map(book => `
                <div class="book-card-profile" style="background: var(--surface-alt); padding: 1rem; border-radius: 12px; border: 1px solid var(--border); display: flex; flex-direction: column; gap: 10px; text-align: center;">
                    
                    <h4 style="font-family: var(--font-heading); font-size: 1.2rem; margin: 0; color: var(--text);">${book.title}</h4>
                    
                    <p style="font-size: 0.9rem; color: var(--text-muted); margin-bottom: 0.5rem;">${book.author || 'Автор неизвестен'}</p>

                    <div style="margin-top: auto; display: flex; gap: 10px; width: 100%;">
                        <!-- Кнопка Читать -->
                        <button onclick="startReadingSession('${book.fileUrl}')" 
                                class="btn btn-primary" style="flex: 1; padding: 8px 12px; font-size: 0.9rem;">
                            Читать
                        </button>

                        <!-- Кнопка Вернуть -->
                        <button onclick="returnBook(${book.id})" 
                                class="btn btn-secondary" style="flex: 1; padding: 8px 12px; font-size: 0.9rem;">
                            Вернуть
                        </button>
                    </div>
                </div>
            `).join('');
        }
    } catch (error) {
        console.error(error);
        list.innerHTML = "<p>Не удалось загрузить книги.</p>";
    }
}

// 3. ФУНКЦИЯ ВОЗВРАТА КНИГИ
async function returnBook(bookId) {
    if(!confirm("Убрать книгу с полки?")) return;
    
    const token = localStorage.getItem('token');
    try {
        const res = await fetch(`${API_URL}/library/return/${bookId}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if(res.ok) {
            loadMyBooks(); 
        }
    } catch(e) { console.error(e); }
}