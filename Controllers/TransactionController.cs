using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceAPI.Data;
using PersonalFinanceAPI.Models;
using System.Security.Claims;

namespace PersonalFinanceAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        // GET: api/transactions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions()
        {
            int userId = GetUserId();
            return await _context.Transactions
                .Where(t => t.UserId == userId)
                .Include(t => t.Wallet)
                .Include(t => t.Category)
                .ToListAsync();
        }

        // POST: api/transactions
        [HttpPost]
        public async Task<ActionResult<Transaction>> PostTransaction(Transaction transaction)
        {
            int userId = GetUserId();
            transaction.UserId = userId;

            // Pastikan dompet yang dipilih benar-benar milik user yang sedang login
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.Id == transaction.WalletId && w.UserId == userId);
            
            if (wallet == null)
            {
                return BadRequest(new { message = "Dompet tidak ditemukan atau bukan milik akun Anda." });
            }

            // Validasi dan Perbarui Saldo Dompet Berdasarkan Jenis Transaksi
            // Type 0 = Pemasukan (Income), Type 1 = Pengeluaran (Expense)
            if ((int)transaction.Type == 1) // atau jika enum Anda bernama Expense, gunakan (int)transaction.Type == ...
            {
                if (wallet.Balance < transaction.Amount)
                {
                    return BadRequest(new { message = $"Saldo dompet '{wallet.Name}' tidak mencukupi." });
                }
                wallet.Balance -= transaction.Amount;
            }
            else if ((int)transaction.Type == 0)
            {
                wallet.Balance += transaction.Amount;
            }

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTransactions), new { id = transaction.Id }, transaction);
        }

        // DELETE: api/transactions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            int userId = GetUserId();
            var transaction = await _context.Transactions
                .Include(t => t.Wallet)
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (transaction == null) return NotFound();

            // Kembalikan/Rollback saldo dompet saat transaksi dihapus
            if (transaction.Wallet != null)
            {
                if ((int)transaction.Type == 1)
                {
                    transaction.Wallet.Balance += transaction.Amount;
                }
                else if ((int)transaction.Type == 0)
                {
                    transaction.Wallet.Balance -= transaction.Amount;
                }
            }

            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}