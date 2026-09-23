// Top-Level-Statement: Der Compiler erzeugt daraus automatisch die Main-Methode.
// "using var" sorgt dafür, dass game.Dispose() am Ende des Scopes garantiert aufgerufen wird
// (gibt GPU-Ressourcen, Fenster und Audio-Geräte frei).
using var game = new CirclesOfAsh.CirclesGame();
game.Run();
