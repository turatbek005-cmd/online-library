    const HOST = "localhost:5283"; 
    const API_URL = `http://${HOST}/api`;

    document.addEventListener('DOMContentLoaded', () => {
        updateUIBalance();
        loadShopShowcase();
    });

    function updateUIBalance() {
        const user = JSON.parse(localStorage.getItem('user'));
        if (user) {
            document.getElementById('userBalance').textContent = user.emeralds || 0;
        }
    }

    async function loadShopShowcase() {
        const content = document.getElementById('shopContent');
        const token = localStorage.getItem('token');
        
        try {
            const response = await fetch(`${API_URL}/shop/showcase`, {
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
                                            <img src="${card.image_url || 'assets/images/card/common/D.jpg'}" class="card-image">
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

    function showCustomConfirm(message) {
        return new Promise((resolve) => {
            const modal = document.getElementById('confirmModal');
            const messageEl = document.getElementById('modalMessage');
            const okBtn = document.getElementById('confirmOk');
            const cancelBtn = document.getElementById('confirmCancel');

            messageEl.textContent = message;
            modal.style.display = 'flex';

            const cleanup = (result) => {
                modal.style.display = 'none';
                okBtn.onclick = null;
                cancelBtn.onclick = null;
                resolve(result);
            };

            okBtn.onclick = () => cleanup(true);
            cancelBtn.onclick = () => cleanup(false);
        });
    }

    async function handlePurchase(cardId, price, cardName) {
        const token = localStorage.getItem('token');
        let user = JSON.parse(localStorage.getItem('user'));

        if (!token || !user) {
            showNotification("Пожалуйста, войдите в аккаунт!", false);
            setTimeout(() => window.location.href = "login.html", 2000);
            return;
        }

        const confirmed = await showCustomConfirm(`Купить артефакт "${cardName}" за ${price} изумрудов?`);
        if (!confirmed) return;

        try {
            const response = await fetch(`${API_URL}/shop/buy-card/${cardId}`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });

            const data = await response.json();

            if (response.ok) {
                showNotification(`Вы успешно приобрели карту: ${cardName}`);
                user.emeralds = data.remainingEmeralds; 
                localStorage.setItem('user', JSON.stringify(user));
                updateUIBalance();
            } else {
                showNotification(data.message || "Ошибка при покупке", false);
            }
        } catch (error) {
            showNotification("Ошибка связи с сервером", false);
        }
    }

    function showNotification(message, isSuccess = true) {
        const modal = document.getElementById('notificationModal');
        const icon = document.getElementById('notificationIcon');
        const title = document.getElementById('notificationTitle');
        const msgEl = document.getElementById('notificationMessage');

        msgEl.textContent = message;
        title.textContent = isSuccess ? "Успех!" : "Внимание";
        icon.textContent = isSuccess ? "🎉" : "⚠️";
        
        modal.style.display = 'flex';
    }

    function closeNotification() {
        document.getElementById('notificationModal').style.display = 'none';
    }