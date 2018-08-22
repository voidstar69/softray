namespace Engine3D
{
    interface Texture3D<T>
    {
        // texture coordinates should probably fall within the unit cube centred at the origin
        T Sample(Vector pos);
    }
}