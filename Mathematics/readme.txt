Ideally, the math project should be below the wpf project

But Debug3DWindow is really useful for visualizing problems and is needed when building/debugging math problems

So a comprimise was made and this physical dll holds both, with each folder representing what should have been its own
dll.  The only reference from math to wpf should be to the Debug3DWindow