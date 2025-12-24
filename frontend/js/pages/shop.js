document.addEventListener('DOMContentLoaded', () => {
    updateUIBalance();
    loadShopShowcase();
});

const SHOP_API_URL = "http://localhost:5283/api";

function updateUIBalance() {
    const user = JSON.parse(localStorage.getItem('user'));
    
    // === ИСПРАВЛЕНИЕ: ID должен быть 'profile-emeralds', как в HTML ===
    const balanceEl = document.getElementById('profile-emeralds');
    
    if (user && balanceEl) {
        balanceEl.textContent = user.emeralds || 0;
    }
}

async function loadShopShowcase() {
    const content = document.getElementById('shopContent');
    const token = localStorage.getItem('token');
    
    try {
        const response = await fetch(`${SHOP_API_URL}/shop/showcase`, {
            headers: { 'Authorization': token ? `Bearer ${token}` : '' }
        });

        if (!response.ok) throw new Error("Ошибка загрузки");
        const cards = await response.json();
        
        if (cards.length === 0) {
            content.innerHTML = '<p style="text-align:center;">Магазин пуст.</p>';
            return;
        }

        const ranks = ['S', 'A', 'B', 'C', 'D', 'E'];
        const rankNames = { 'S': 'Легендарные', 'A': 'Эпические', 'B': 'Редкие', 'C': 'Необычные', 'D': 'Обычные', 'E': 'Базовые' };

        let finalHtml = '';
        ranks.forEach(r => {
            const filteredCards = cards.filter(c => c.rank.toUpperCase() === r);
            if (filteredCards.length > 0) {
                finalHtml += `
                    <section class="rank-section rank-${r.toLowerCase()}">
                        <div class="rank-header">
                            <span class="rank-letter">${r}</span>
                            <span class="rank-title">${rankNames[r]}</span>
                        </div>
                        <div class="cards-grid">
                            ${filteredCards.map(card => `
                                <div class="shop-card scroll-reveal">
                                    <div class="card-image-container">
                                        <img src="${card.image_url || 'assets/images/placeholder.jpg'}" class="card-image" onerror="this.src='https://via.placeholder.com/200x300?text=Card'">
                                    </div>
                                    <div class="card-info">
                                        <h3 class="card-name">${card.name}</h3>
                                        <div class="card-price">💎 ${card.price}</div>
                                        <button class="btn-buy" onclick="handlePurchase(${card.id}, ${card.price}, '${card.name}')">Купить</button>
                                    </div>
                                </div>
                            `).join('')}
                        </div>
                    </section>`;
            }
        });
        content.innerHTML = finalHtml;
    } catch (error) {
        content.innerHTML = '<p style="text-align:center; color:var(--status-unavailable);">Ошибка подключения к серверу.</p>';
    }
}

// === Функция покупки ===
window.handlePurchase = function(cardId, price, cardName) {
    // Вызываем глобальную функцию buyCard из script.js, если она есть
    if (typeof window.buyCard === 'function') {
        window.buyCard(cardId, price, cardName);
        // Обновляем баланс локально сразу после вызова (хотя buyCard тоже это делает)
        setTimeout(updateUIBalance, 500); 
    } else {
        console.error("Ошибка: script.js не загружен или в нем нет функции buyCard");
    }
};