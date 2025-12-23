const API_URL = "http://localhost:5283/api";
    let allBooks = [];

    document.addEventListener('DOMContentLoaded', fetchBooks);

    async function fetchBooks() {
        const grid = document.getElementById('booksGrid');
        grid.innerHTML = '<p style="text-align: center; width: 100%;">Открываем архивы...</p>';

        try {
            const response = await fetch(`${API_URL}/books`);
            if (!response.ok) throw new Error();
            allBooks = await response.json();
            renderBooks(allBooks);
        } catch (error) {
            grid.innerHTML = '<p style="color:red; text-align: center; width: 100%;">Ошибка связи с сервером. Пожалуйста, убедитесь, что бэкенд запущен.</p>';
        }
    }

    function renderBooks(books) {
        const grid = document.getElementById('booksGrid');
        grid.innerHTML = '';

        if (books.length === 0) {
            grid.innerHTML = '<p style="text-align: center; width: 100%;">Книги не найдены.</p>';
            return;
        }

        books.forEach((book, index) => {
            const card = document.createElement('div');
            card.className = 'book-card fade-in';
            card.style.animationDelay = `${index * 0.05}s`; // Плавное появление по очереди
            
            // Если нет обложки, используем стильную заглушку
            const coverSrc = (book.coverImage && book.coverImage.length > 5) 
                ? book.coverImage 
                : 'assets/images/placeholder-book.jpg';

            card.innerHTML = `
                <div class="book-cover-wrapper" onclick="goToDetails(${book.id})">
                    <img src="${coverSrc}" class="book-cover" alt="${book.title}" onerror="this.src='https://via.placeholder.com/200x300?text=No+Cover'">
                </div>
                <div class="book-title" onclick="goToDetails(${book.id})">${book.title}</div>
            `;
            grid.appendChild(card);
        });
    }

    // Переход на новую страницу деталей
    function goToDetails(id) {
        window.location.href = `book-details.html?id=${id}`;
    }

    // Поиск в реальном времени
    document.getElementById('searchInput').addEventListener('input', (e) => {
        const val = e.target.value.toLowerCase();
        const filtered = allBooks.filter(b => 
            b.title.toLowerCase().includes(val) || 
            b.author.toLowerCase().includes(val)
        );
        renderBooks(filtered);
    });