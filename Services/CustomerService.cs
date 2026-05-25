using StorePitOne.Models;

namespace StorePitOne.Services
{
    public class CustomerService
    {
        private List<Customer> customers = new();

        public List<Customer> GetAll()
        {
            return customers;
        }

        public void Add(Customer customer)
        {
            customer.Id = customers.Count + 1;
            customers.Add(customer);
        }
    }
}