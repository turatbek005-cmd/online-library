const API_URL = "http://localhost:5283/api";

    // === РЕКОМЕНДАЦИИ ===
    async function renderRecommendations() {
        const grid = document.getElementById('recommendationsGrid');
        if(!grid) return;

        try {
            // В реальном проекте здесь будет fetch к API
            // Для демонстрации используем заглушку, если API недоступен
            let topBooks = [];
            try {
                const response = await fetch(`${API_URL}/books/top`);
                if(response.ok) topBooks = await response.json();
            } catch(e) {
                console.log("API недоступен, используем демо-данные");
            }
            
            // Демо данные, если база пуста или API выключен
            if (topBooks.length === 0) {
                 topBooks = [
                    { id: 1, title: "Мастер и Маргарита", author: "Михаил Булгаков", coverImage: null, rating: 5.0 },
                    { id: 2, title: "Преступление и наказание", author: "Фёдор Достоевский", coverImage: null, rating: 4.9 },
                    { id: 3, title: "1984", author: "Джордж Оруэлл", coverImage: null, rating: 4.8 }
                 ];
            }

            grid.innerHTML = topBooks.map(book => `
                <div class="book-card scroll-reveal" onclick="window.location.href='book-details.html?id=${book.id}'">
                    <div class="book-cover">
                         ${book.coverImage ? `<img src="${book.coverImage}" style="width:100%; height:100%; object-fit:cover; border-radius:inherit;">` : '📖'}
                    </div>
                    <div class="book-info">
                        <div class="book-title">${book.title}</div>
                        <div class="book-author">${book.author}</div>
                        <div style="color: var(--accent-gold); font-size: 0.9rem; margin-top: 0.5rem;">★ ${book.rating || 'New'}</div>
                    </div>
                </div>
            `).join('');
            
            setupScrollAnimations();

        } catch (e) {
            grid.innerHTML = '<p style="text-align:center; color: var(--text-muted);">Не удалось загрузить рекомендации.</p>';
        }
    }

    // === ТЕКУЩИЕ КНИГИ (ПОЛКА) ===
    async function renderCurrentShelf() {
      const shelf = document.getElementById('currentShelf');
      const empty = document.getElementById('shelfEmpty');
      
      // Берем данные из LocalStorage или API (здесь для примера LocalStorage как в оригинале, но с API)
      const token = localStorage.getItem('token');
      let books = [];
      
      if(token) {
          try {
              const res = await fetch(`${API_URL}/library/my-books`, {
                  headers: { 'Authorization': `Bearer ${token}` }
              });
              if(res.ok) books = await res.json();
          } catch(e) { console.error(e); }
      }

      if (books.length === 0) {
        shelf.style.display = 'none';
        empty.style.display = 'block';
        return;
      }
      
      empty.style.display = 'none';
      shelf.style.display = 'grid';
      
      // Показываем только 2 последние
      const recentBooks = books.slice(0, 2);
      
      shelf.innerHTML = recentBooks.map(book => `
        <div class="scroll-reveal" style="background: var(--surface-alt); border-radius: 16px; padding: 1.5rem; display: flex; align-items: center; gap: 1.5rem; border: 1px solid var(--border); box-shadow: var(--shadow);">
          <div style="font-size: 2.5rem; background: rgba(139, 69, 19, 0.1); width: 60px; height: 60px; display: flex; align-items: center; justify-content: center; border-radius: 12px;">📖</div>
          <div style="flex-grow: 1;">
            <h3 style="margin-bottom: 0.25rem; color: var(--text); font-family: var(--font-heading); font-size: 1.2rem;">${book.title}</h3>
            <p style="color: var(--text-muted); font-size: 0.9rem;">${book.author}</p>
          </div>
          <button class="btn btn-secondary" onclick="continueReading('${book.title}')" style="padding: 0.5rem 1.2rem; font-size: 0.9rem;">
            Продолжить
          </button>
        </div>
      `).join('');
      
      setupScrollAnimations();
    }
    
    // Новая функция с красивым окном
    function continueReading(bookTitle) {
      showNotification(`Открываем последнюю страницу книги "${bookTitle}"...`, true);
      // Логика перехода в читалку
    }

    // === УВЕДОМЛЕНИЯ ===
    function showNotificationBanner() {
      const banner = document.getElementById('notificationBanner');
      const notifications = [
        '✨ Новая книга в каталоге: "Сто лет одиночества"',
        '🌟 Специальная подборка: Осеннее чтение',
        '💎 Получите ежедневный бонус в профиле!'
      ];
      
      if (Math.random() > 0.6) {
        banner.textContent = notifications[Math.floor(Math.random() * notifications.length)];
        banner.style.display = 'block';
        setTimeout(() => { banner.style.display = 'none'; }, 5000);
      }
    }

    // === МОДАЛЬНОЕ ОКНО ===
    function showNotification(message, isSuccess = true) {
        const modal = document.getElementById('notificationModal');
        const icon = document.getElementById('notificationIcon');
        const title = document.getElementById('notificationTitle');
        const msgEl = document.getElementById('notificationMessage');

        msgEl.textContent = message;
        title.textContent = isSuccess ? "Приятного чтения" : "Внимание";
        icon.textContent = isSuccess ? "📖" : "⚠️";
        
        modal.style.display = 'flex';
    }

    function closeNotification() {
        document.getElementById('notificationModal').style.display = 'none';
    }

    // === АНИМАЦИИ ===
    function setupScrollAnimations() {
      const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            entry.target.style.animationPlayState = 'running';
            entry.target.classList.add('visible'); // Для CSS запасной вариант
          }
        });
      }, { threshold: 0.1 });
      
      document.querySelectorAll('.scroll-reveal').forEach(el => observer.observe(el));
    }

    // === ИНИЦИАЛИЗАЦИЯ ===
    document.addEventListener('DOMContentLoaded', () => {
      renderRecommendations();
      renderCurrentShelf();
      showNotificationBanner();
      
      // Плавный хедер
      window.addEventListener('scroll', () => {
        const header = document.querySelector('.header');
        header.classList.toggle('scrolled', window.scrollY > 50);
      });
    });