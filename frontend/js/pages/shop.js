document.addEventListener('DOMContentLoaded', () => {
    updateUIBalance();
    loadShopShowcase();
});

// Настройка URL (должна совпадать с твоим портом)
const SHOP_API_URL = "http://localhost:5283/api";

function updateUIBalance() {
    const user = JSON.parse(localStorage.getItem('user'));
    // Обновляем виджет баланса, если он есть
    const balanceEl = document.getElementById('userBalance');
    if (user && balanceEl) {
        balanceEl.textContent = user.emeralds || 0;
    }
}

// Загрузка витрины
async function loadShopShowcase() {
    const content = document.getElementById('shopContent');
    
    try {
        const response = await fetch(`${SHOP_API_URL}/shop/showcase`);

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
        console.error(error);
        content.innerHTML = '<p style="text-align:center; color:red;">Ошибка подключения к серверу.</p>';
    }
}

// Покупка (Используем глобальный showModal из script.js)
window.handlePurchase = function(cardId, price, cardName) {
    const user = JSON.parse(localStorage.getItem('user'));
    const token = localStorage.getItem('token');

    // 1. Проверка входа
    if (!user || !token) {
        showModal({ 
            title: "Ошибка", 
            text: "Войдите в аккаунт, чтобы покупать карты!", 
            showCancel: false 
        });
        return;
    }

    // 2. Проверка баланса (визуально)
    if ((user.emeralds || 0) < price) {
        showModal({ 
            title: "Не хватает средств", 
            text: `У вас ${user.emeralds} 💎, а нужно ${price}.`, 
            icon: "💎",
            showCancel: false 
        });
        return;
    }

    // 3. Вызов универсального окна подтверждения
    showModal({
        title: "Покупка",
        text: `Купить артефакт "${cardName}" за ${price} 💎?`,
        icon: "📜🪶",
        onConfirm: async () => {
            try {
                const response = await fetch(`${SHOP_API_URL}/shop/buy-card/${cardId}`, {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                });

                const data = await response.json();

                if (response.ok) {
                    // Обновляем данные юзера
                    user.emeralds = data.remainingEmeralds; 
                    localStorage.setItem('user', JSON.stringify(user));
                    
                    updateUIBalance(); // Обновляем цифру в углу
                    
                    showModal({ 
                        title: "Успешно!", 
                        text: `Вы получили карту: ${cardName}`, 
                        icon: "✨", 
                        showCancel: false 
                    });
                } else {
                    showModal({ title: "Ошибка", text: data.message || "Ошибка при покупке", showCancel: false });
                }
            } catch (error) {
                console.error(error);
                showModal({ title: "Ошибка", text: "Сбой сети", showCancel: false });
            }
        }
    });
};