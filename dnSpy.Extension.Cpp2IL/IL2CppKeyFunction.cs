namespace Cpp2ILAdapter;

public enum IL2CppKeyFunction : byte
{
    IL2CppCodegenInitializeMethod,
    IL2CppRuntimeClassInit,
    IL2CppObjectNew,
    IL2CppArrayNewSpecific,
    IL2CppTypeGetObject,
    IL2CppResolveIcall,
    IL2CppStringNew,
    IL2CppValueBox,
    IL2CppObjectUnbox,
    IL2CppRaiseException,
    IL2CppVmObjectIsInst,
    AddrPInvokeLookup
}