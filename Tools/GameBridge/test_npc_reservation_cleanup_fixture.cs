using System;
interface IRebirthNpcExternalInventoryReservation:IDisposable{}
class Reservation:IRebirthNpcExternalInventoryReservation {public bool Fail;public void Dispose(){if(Fail)throw new InvalidOperationException("rollback failed");}}
class Test {
 static int Released;static Guid Last;
 static void ReleaseInFlight(Guid id, long generation){Released++;Last=id;}
 // SOURCE
 static void Main(){Guid id=Guid.NewGuid();ReleaseReservation(null,id,1);ReleaseReservation(new Reservation(),id,1);bool failed=false;try{ReleaseReservation(new Reservation{Fail=true},id,1);}catch(InvalidOperationException){failed=true;}if(!failed||Released!=3||Last!=id)throw new Exception("cleanup failed");Console.WriteLine("PASS: null, normal and throwing disposal all release matching transaction; exception preserved");}
}
