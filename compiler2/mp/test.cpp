#include <gmp.h>
#include <iostream>

int main()
{
   //using namespace boost::multiprecision;
   //cpp_rational w(1, 3);
   //boost::multiprecision::
   //std::cout << (w * 3) << std::endl;

   mpz_t a;
   mpz_init_set_str(a, "123456789", 10);
   std::cout << a << std::endl;
   mpz_clear(a);

   return 0;
}